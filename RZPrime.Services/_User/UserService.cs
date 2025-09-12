using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nethereum.Signer;
using Nethereum.Util;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509;
using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Services._Order;
using RZPrime.Services._User._Hub;
using RZPrime.Services._User.DTOs.Results;
using RZPrime.Services._User.DTOs.Storages;
using RZPrime.Services._User.DTOs.Updates;
using RZPrime.Services._UserStage;
using RZPrime.Services._UserStage.DTOs.Settings;
using RZPrime.Utilities.Constants;
using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions;
using RZPrime.Utilities.Exceptions.Common;
using RZPrime.Utilities.Services;
using RZPrime.Utilities.Services.Contracts;
using RZPrime.Utilities.Utilities;
using System.Security.Claims;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._User
{
    public class UserService(
        IRandomService _randomService,
        JwtServiceSettings _jwtSettings,
        IJwtService _jwtService,
        IOrderRepository _orderRepository,
        ILogger<UserService> _logger,
        IUserStageService _userStageService,
        IUserRepository _userRepository,
        UserStageSetting _userStageSetting, IHubContext<NonceNotifyHub> _hubContext,
        UserAuthStorage _userAuthStorage) : IUserService, IScopedDependency
    {
        public NonceResult GetNonce(NonceRequest update, string ip)
        {
            ValidateClientInfo(update.ClientId, update.ClientSecret);
            var walletAddress = ValidateAndConvertToChecksumAddress(update.WalletAddress);
            var random = _randomService.GetSecureAlphaNumericString(6);
            var newNonce = random + Guid.NewGuid().ToString("N");
            var newUserAuthData = new UserAuthData
            {
                Nonce = newNonce,
                WalletAddress = walletAddress,
                GeneratedMoment = DateTime.UtcNow,
                IP = ip,
                IsVerified = true,
                DeviceId = newNonce
            };

            _userAuthStorage.AddItem(newNonce, newUserAuthData);

            return new NonceResult
            {
                ExpireMoment = newUserAuthData.GeneratedMoment.AddSeconds(10),
                Nonce = newNonce,
                Message = $"Please sign this message to authenticate with RZPrime: {newNonce}"
            };
        }

        public async Task<NonceResult> GetNonceForApp(AppNonceRequest update, string ip)
        {
            ValidateClientInfo(update.ClientId, update.ClientSecret);
            if (update.Marker.IsNullOrEmpty()) throw new BadRequestException("Marker is invalid!");

            var walletAddress = ValidateAndConvertToChecksumAddress(update.WalletAddress);
            var random = _randomService.GetSecureAlphaNumericString(6);
            var newNonce = random + Guid.NewGuid().ToString("N");

            await SyncMobileAuthRequestAsync(update.Marker, update.WalletAddress);
            var newUserAuthData = new UserAuthData
            {
                Nonce = newNonce,
                WalletAddress = walletAddress,
                GeneratedMoment = DateTime.UtcNow,
                IP = ip,
                IsVerified = true,
                DeviceId = update.Marker,
            };

            _userAuthStorage.AddItem(newUserAuthData.Nonce, newUserAuthData);

            return new NonceResult
            {
                ExpireMoment = newUserAuthData.GeneratedMoment.AddMinutes(5),
                Nonce = newUserAuthData.Nonce,
                Code = null,
                ShouldVerify = false,
                Message = $"Please sign this message to authenticate with RZPrime: {newUserAuthData.Nonce}"
            };
        }

        public async Task<NonceResult> GetNonceForWeb(WebNonceRequest update, string ip)
        {
            ValidateClientInfo(update.ClientId, update.ClientSecret);
            //var walletAddress = ValidateAndConvertToChecksumAddress(update.WalletAddress);

            var isExclusive = IsExclusiveWallets(update.WalletAddress);

            var random = _randomService.GetSecureAlphaNumericString(6);
            var newNonce = random + Guid.NewGuid().ToString("N");
            var code = _randomService.GetSecureAlphaNumericString(6).ToUpper();


            var newUserAuthData = new UserAuthData
            {
                Nonce = newNonce,
                WalletAddress = isExclusive ? update.WalletAddress : null,
                GeneratedMoment = DateTime.UtcNow,
                IP = ip,
                IsVerified = isExclusive,
                Code = isExclusive ? null : code,
                DeviceId = newNonce
            };

            _userAuthStorage.AddItem(newNonce, newUserAuthData);

            return new NonceResult
            {
                ExpireMoment = newUserAuthData.GeneratedMoment.AddMinutes(5),
                Nonce = newNonce,
                Code = newUserAuthData.Code,
                ShouldVerify = !isExclusive,
                Message = isExclusive ? $"Please sign this message to authenticate with RZPrime: {newNonce}" : $"Please Scan QR code with your phone."
            };
        }

        public async Task<bool> ActivateNonceAsync(ActivateNonceRequest update)
        {

            ValidateClientInfo(update.ClientId, update.ClientSecret);
            if (update.Code.IsNullOrEmpty() && update.Nonce.IsNullOrEmpty())
            {
                throw new BadRequestException("One of Code or Nonce is Required!");
            }

            if (!update.Nonce.IsNullOrEmpty())
            {
                return await ActivateWithNonceAsync(update);
            }

            if (!update.Code.IsNullOrEmpty())
            {
                return await ActivateWithCodeAsync(update);
            }

            throw new BadRequestException();
        }

        private async Task<bool> ActivateWithCodeAsync(ActivateNonceRequest update)
        {
            var code = update.Code;
            var userAuthData = _userAuthStorage.GetItemByCode(update.Code) ?? throw new NonceNotFoundException();
            var nonce = userAuthData.Nonce;

            if (DateTime.UtcNow - userAuthData.GeneratedMoment > TimeSpan.FromMinutes(5))
            {
                _userAuthStorage.RemoveItem(nonce);
                throw new BadRequestException("nonce expired!");
            }
            if (!await CheckExistingWalletInDatabaseAsync(update.WalletAddress, update.Marker))
            {
                throw new NotFoundException("Please register with your phone!");
            }

            _userAuthStorage.VerifyNonce(nonce, update.Marker, update.WalletAddress);

            await _hubContext.Clients.Group(nonce)
                .SendAsync("notifyNonceMessage", $"Please sign this message to authenticate with RZPrime: {nonce}");
            return true;
        }

        private async Task<bool> ActivateWithNonceAsync(ActivateNonceRequest update)
        {
            var nonce = update.Nonce;
            var userAuthData = _userAuthStorage.GetItem(update.Nonce) ?? throw new NonceNotFoundException();

            if (DateTime.UtcNow - userAuthData.GeneratedMoment > TimeSpan.FromMinutes(5))
            {
                _userAuthStorage.RemoveItem(nonce);
                throw new BadRequestException("nonce expired!");
            }
            if (!await CheckExistingWalletInDatabaseAsync(update.WalletAddress, update.Marker))
            {
                throw new NotFoundException("Please register with your phone!");
            }

            _userAuthStorage.VerifyNonce(nonce, update.Marker, update.WalletAddress);

            await _hubContext.Clients.Group(nonce)
                .SendAsync("notifyNonceMessage", $"Please sign this message to authenticate with RZPrime: {nonce}");
            return true;
        }



        private bool IsExclusiveWallets(string walletAdress)
        {

            if (walletAdress.IsNullOrEmpty())
                return false;

            var exclusiveWallets = new List<string>()
            {
                "0xD05Ac51B1113da21E22A595612767e7C3F6B0F00",
                "0x798457be80878b1f132e3A516b4b44E197CE3076",
                "0x55304995141dc954d7cb8bA77302b16fe7d629A4"
            };

            if (exclusiveWallets.Contains(walletAdress))
            {
                return true;
            }

            return false;
        }

        private async Task<bool> CheckExistingWalletInDatabaseAsync(string walletAddress, string deviceId)
        {
            return await _userRepository.
                ExistsAsync(q => q.WalletAddress.Equals(walletAddress, StringComparison.CurrentCultureIgnoreCase)
            && q.Devices.Any(q => q.DeviceId.Equals(deviceId, StringComparison.CurrentCultureIgnoreCase)));
        }

        private async Task SyncMobileAuthRequestAsync(string deviceId, string walletAddress)
        {
            var usersWithSameDeviceId = await _userRepository.AsQueryable()
                .Where(q => q.Devices.Any(q => q.DeviceId.Equals(deviceId, StringComparison.CurrentCultureIgnoreCase))).ToListAsync();

            if (usersWithSameDeviceId.Count > 1)
            {
                _logger.LogError($"deviceId : {deviceId} is Duplicated in system");
                throw new BaseException("There is a problem with your Auth data!");
            }

            var user = usersWithSameDeviceId.FirstOrDefault();
            if (user != null)
            {
                if (user.WalletAddress.ToLower() != walletAddress.ToLower())
                {
                    throw new BadRequestException();
                }
            }
        }



        /// <summary>
        /// this method is for get jwt token
        /// here check the nonce and wallet and signature with  nethereium
        /// throw error if data is not valid
        /// generate jwt token for valid data for login and update nonce storage
        /// </summary>
        /// <param name="update"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        public async Task<ActionResult> GetToken(NonceVerification update, string ip)
        {
            ValidateClientInfo(update.ClientId, update.ClientSecret);

            var userAuthData = ValidateNonce(update.Nonce, update.WalletAddress);

            if (!userAuthData.IsVerified) throw new BadRequestException("You are not verified with your phone!");

            var message = $"Please sign this message to authenticate with RZPrime: {update.Nonce}";
            VerifySignature(message, update.Signature, userAuthData.WalletAddress);

            var user = await GetOrCreateUserAsync(userAuthData.WalletAddress, userAuthData.DeviceId);

            _userAuthStorage.RemoveItem(update.Nonce);

            return Authenticate(user);
        }




        /// <summary>
        /// this method use for get user stats in each available stage
        /// </summary>
        /// <param name="userPublicKey"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public async Task<List<GetUserStatsResult>> GetUserStatsAsync(string userPublicKey, string walletAddress)
        {
            var result = new List<GetUserStatsResult>();

            var stages = await _userStageService.GetUserStagesByWalletAddressForInternalUsage(walletAddress, userPublicKey);

            var registeredOrders = await GetUserRegisteredOrderForEachStageAsync(userPublicKey, walletAddress);

            foreach (var stage in stages)
            {
                var stageSetting = GetStageSetting(stage.Stage);
                decimal LoanAmount = 0;

                if (registeredOrders.TryGetValue(stage.UserStageId, out decimal value))
                {
                    LoanAmount = value;
                }

                result.Add(new GetUserStatsResult
                {
                    Stage = stage.Stage,
                    AvailableLoanAmount = stageSetting.MaximumBuyAmount - LoanAmount,
                    SumOfMining = 0,
                    AvailableDropCount = stage.AvailableDrop
                });
            }

            return result;
        }




        #region Private Methods
        /// <summary>
        /// Validates the provided nonce and associated wallet address.
        /// </summary>
        /// <param name="Nonce">The unique nonce value issued to the user for authentication.</param>
        /// <param name="walletAddress">The wallet address provided by the client.</param>
        /// <returns>The <see cref="UserAuthData"/> associated with the nonce if validation succeeds.</returns>
        /// <exception cref="NonceNotFoundException">Thrown if the nonce does not exist in the storage.</exception>
        /// <exception cref="BadRequestException">
        /// Thrown if the nonce has expired or if the provided wallet address does not match the one stored for this nonce.
        /// </exception>
        /// <remarks>
        /// This method ensures:
        /// 1. The nonce exists in the <see cref="UserAuthStorage"/>.
        /// 2. The nonce has not expired (valid for 10 seconds from creation).
        /// 3. The provided wallet address matches the one originally associated with the nonce.
        /// </remarks>
        private UserAuthData ValidateNonce(string Nonce, string walletAddress)
        {
            var userAuthData = _userAuthStorage.GetItem(Nonce) ?? throw new NonceNotFoundException();

            if (DateTime.UtcNow - userAuthData.GeneratedMoment > TimeSpan.FromSeconds(300))
            {
                _userAuthStorage.RemoveItem(Nonce);
                throw new BadRequestException("nonce expired!");
            }

            if (!string.Equals(userAuthData.WalletAddress, walletAddress, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Wallet address mismatch for nonce");

            return userAuthData;
        }


        /// <summary>
        /// Verifies that the provided cryptographic signature matches the given wallet address for the specified message.
        /// </summary>
        /// <param name="message">The original message that was signed by the wallet.</param>
        /// <param name="signatureHex">The hexadecimal signature generated by the wallet for the message.</param>
        /// <param name="walletAddress">The expected wallet address that allegedly signed the message.</param>
        /// <exception cref="BadRequestException">
        /// Thrown if:
        /// <list type="bullet">
        /// <item>The message, signature, or wallet address is null, empty, or whitespace.</item>
        /// <item>The recovered address from the signature does not match the provided wallet address.</item>
        /// </list>
        /// </exception>
        /// <exception cref="BaseException">
        /// Thrown if there is an error during the Web3/Ethereum signature recovery process.
        /// </exception>
        /// <remarks>
        /// This method uses <see cref="EthereumMessageSigner"/> from the Nethereum library to recover the address
        /// from the provided message and signature. It ensures that the signature is valid for the given wallet
        /// address according to the Ethereum (EVM) signing standard.
        /// </remarks>
        private void VerifySignature(string message, string signatureHex, string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(message) ||
                string.IsNullOrWhiteSpace(signatureHex) ||
                string.IsNullOrWhiteSpace(walletAddress))
                throw new BadRequestException("invalid input!");

            try
            {
                var signer = new EthereumMessageSigner();

                var recovered = signer.EncodeUTF8AndEcRecover(message, signatureHex);

                var isVerified = string.Equals(recovered, walletAddress, StringComparison.OrdinalIgnoreCase);
                if (!isVerified) throw new BadRequestException("Invalid signature for wallet");
            }
            catch
            {
                throw new BaseException("error in web3 network!");
            }
        }


        /// <summary>
        /// for validate the ClientInformation , OAuth2 verification
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        /// <exception cref="BadRequestException"></exception>
        private void ValidateClientInfo(string clientId, string clientSecret)
        {
            if (!clientId.HasValue() ||
                !clientSecret.HasValue() ||
                !_jwtSettings.ClientInfo.ContainsKey(clientId.ToLower()) ||
                !_jwtSettings.ClientInfo[clientId.ToLower()].Equals(clientSecret, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException(ApiResultStatusCode.OAuth.ToDisplay());
        }


        /// <summary>
        /// this method use for creating jwt
        /// </summary>
        /// <param name="tabletUniqeId"></param>
        /// <param name="tabletData"></param>
        /// <returns></returns>
        private ActionResult Authenticate(Domain.Collections.User user)
           => new JsonResult(_jwtService.Generate(GetClaimsAsync(user)));


        /// <summary>
        /// for create the cliams of jwt
        /// </summary>
        /// <param name="tabletUniqeId"></param>
        /// <param name="tabletData"></param>
        /// <returns></returns>
        /// <exception cref="BaseException"></exception>
        private IEnumerable<Claim> GetClaimsAsync(Domain.Collections.User user)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new(Claims.WalletAddress.ToDisplay(),user.WalletAddress ?? "no wallet"),
                    new(Claims.PublicKey.ToDisplay(),user.UserPublicKey),
                    new(Claims.SecurityStamp.ToDisplay(),user.SecurityStamp),
                    new(Claims.UserType.ToDisplay(),user.Role == UserRole.Customer ? UserType.User.ToString() : UserType.Admin.ToString()),
                };

                claims.AddRange(user.Permissions.Select(permission =>
                    new Claim(Claims.Permission.ToDisplay(), permission)));

                return claims;
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }



        /// <summary>
        /// for get or create user
        /// if wallet exists in db that means user is exists
        /// if does not exists should create a new user
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        private async Task<Domain.Collections.User> GetOrCreateUserAsync(string walletAddress, string deviceId)
        {

            if (deviceId.IsNullOrEmpty()) throw new BadRequestException("Marker is invalid!");

            walletAddress = walletAddress.Trim();

            var userWithDeviceId = await _userRepository.AsQueryable()
                .Where(q => q.Devices.Any(q => q.DeviceId.Equals(deviceId, StringComparison.CurrentCultureIgnoreCase))).ToListAsync();

            if (userWithDeviceId.Count > 1)
            {
                throw new BaseException();
            }


            var user = await _userRepository.FindOneAsync(q => q.WalletAddress.Equals(walletAddress, StringComparison.CurrentCultureIgnoreCase));
            if (user == null)
            {
                if (userWithDeviceId != null && userWithDeviceId.Count > 0)
                {
                    throw new BadRequestException("Mismatch data!");
                }
                var newUser = new Domain.Collections.User
                {
                    WalletAddress = walletAddress,
                    Role = UserRole.Customer,
                    Permissions = [],
                    UserName = null,
                    PasswordHash = null,
                    LoginDates = [DateTime.UtcNow],
                    Devices = [new DeviceData {
                        DeviceId = deviceId,
                    }]
                };

                await _userRepository.InsertOneAsync(newUser);
                await _userStageService.InitializeUserStageAsync(walletAddress, newUser.UserPublicKey);

                return newUser;
            }
            else
            {
                if (user.Devices.Any(q => q.DeviceId.Equals(deviceId, StringComparison.CurrentCultureIgnoreCase)))
                {
                    AddLoginDateToUser(user);
                    await _userRepository.ReplaceOneAsync(user);
                    return user;
                }
                else
                {

                    if (userWithDeviceId != null && userWithDeviceId.Count > 0)
                    {
                        throw new BadRequestException("Wrong Auth data!");
                    }

                    AddLoginDateToUser(user);
                    user.Devices.Add(new DeviceData
                    {
                        DeviceId = deviceId
                    });

                    await _userRepository.ReplaceOneAsync(user);
                    return user;
                }
            }

        }


        /// <summary>
        /// for adding last login time, just keep last 20 record
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <exception cref="BaseException"></exception>
        private static Domain.Collections.User AddLoginDateToUser(Domain.Collections.User user)
        {
            try
            {
                if (user.LoginDates == null || user.LoginDates.Count == 0)
                {
                    user.LoginDates = new List<DateTime> { DateTime.UtcNow };
                    return user;
                }

                user.LoginDates.Add(DateTime.UtcNow);

                var orderedDates = user.LoginDates
                    .OrderByDescending(x => x)
                    .Take(20)
                    .ToList();

                user.LoginDates = orderedDates;

                return user;
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }

        }


        /// <summary>
        /// for getting stage setting by stage type enum
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private StageSetting GetStageSetting(UserStageType stage)
        {
            return stage switch
            {
                UserStageType.Regular => _userStageSetting.Regular,
                UserStageType.Gold => _userStageSetting.Gold,
                UserStageType.Premium => _userStageSetting.Premium,
                UserStageType.X => _userStageSetting.X,
                _ => throw new BadRequestException("wrong stage!"),
            };
        }



        /// <summary>
        /// this method use for get user registered order grouped by userStageId
        /// </summary>
        /// <param name="userPublicKey"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        private async Task<Dictionary<string, decimal>> GetUserRegisteredOrderForEachStageAsync(
              string userPublicKey,
              string walletAddress)
        {
            var groupedResults = await _orderRepository.AsQueryable()
                .Where(q => q.UserPublicKey == userPublicKey && q.WalletAddress == walletAddress)
                .Where(q => q.State == OrderState.Registered)
                .GroupBy(q => q.UserStageId)
                .Select(g => new
                {
                    UserStageId = g.Key,
                    LoanAmount = g.Sum(o => o.LoanAmount)
                })
                .ToListAsync();

            var result = groupedResults.ToDictionary(
                x => x.UserStageId,
                x => x.LoanAmount);

            return result;
        }



        private string ValidateAndConvertToChecksumAddress(string address)
        {
            var addressUtil = new AddressUtil();
            if (!addressUtil.IsValidAddressLength(address) || !addressUtil.IsChecksumAddress(address) && !address.ToLower().Equals(address))
            {
                if (!addressUtil.IsValidEthereumAddressHexFormat(address))
                {
                    throw new BadRequestException($"Invalid address format: {address}");
                }
                return addressUtil.ConvertToChecksumAddress(address);
            }
            return address;
        }

        #endregion
    }
}




///// <summary>
///// this method used for getting a nonce
///// </summary>
///// <param name="update"></param>
///// <param name="ip"></param>
///// <returns></returns>
//public async Task<NonceResult> GetNonce(NonceRequest update, string ip)
//{
//    ValidateClientInfo(update.ClientId, update.ClientSecret);
//    var walletAddress = ValidateAndConvertToChecksumAddress(update.WalletAddress);
//    var random = _randomService.GetSecureAlphaNumericString(6);
//    var newNonce = random + Guid.NewGuid().ToString("N");


//    var newUserAuthData = new UserAuthData
//    {
//        Nonce = newNonce,
//        WalletAddress = walletAddress,
//        GeneratedMoment = DateTime.UtcNow,
//        IP = ip,
//        IsVerified = true
//    };

//    if (update.Marker.Length == 5)
//    {
//        return await GetNonceWithTwoStepAsync(newUserAuthData, update);
//    }

//    _userAuthStorage.AddItem(newNonce, newUserAuthData);

//    return new NonceResult
//    {
//        ExpireMoment = newUserAuthData.GeneratedMoment.AddMinutes(5),
//        Nonce = newNonce,
//        ShouldVerify = false,
//        Message = $"Please sign this message to authenticate with RZPrime: {newNonce}"
//    };
//}



//private async Task<NonceResult> GetNonceWithTwoStepAsync(UserAuthData newUserAuthData, NonceRequest update)
//{
//    var isMobile = !string.IsNullOrEmpty(update.Marker);
//    var shouldVerify = !isMobile;

//    if (isMobile)
//    {
//        var isNew = await SyncMobileAuthRequestAsync(update.Marker, update.WalletAddress);
//        newUserAuthData.IsNew = isNew;
//        newUserAuthData.DeviceId = update.Marker;
//        newUserAuthData.IsVerified = true;
//    }
//    else
//    {
//        newUserAuthData.IsVerified = false;
//        newUserAuthData.DeviceId = null;
//    }

//    _userAuthStorage.AddItem(newUserAuthData.Nonce, newUserAuthData);

//    return new NonceResult
//    {
//        ExpireMoment = newUserAuthData.GeneratedMoment.AddMinutes(5),
//        Nonce = newUserAuthData.Nonce,
//        ShouldVerify = shouldVerify,
//        Message = shouldVerify ? "Please Scan the QR code with your phone." : $"Please sign this message to authenticate with RZPrime: {newUserAuthData.Nonce}"
//    };
//}


//old methods


///// <summary>
///// this method used for getting a nonce
///// </summary>
///// <param name="update"></param>
///// <param name="ip"></param>
///// <returns></returns>
//public NonceResult GetNonce(NonceRequest update, string ip)
//{
//    ValidateClientInfo(update.ClientId, update.ClientSecret);
//    var walletAddress = ValidateAndConvertToChecksumAddress(update.WalletAddress);
//    var random = _randomService.GetSecureAlphaNumericString(6);
//    var newNonce = random + Guid.NewGuid().ToString("N");
//    var newUserAuthData = new UserAuthData
//    {
//        Nonce = newNonce,
//        WalletAddress = walletAddress,
//        GeneratedMoment = DateTime.UtcNow,
//        IP = ip,
//        IsUsed = false
//    };

//    _userAuthStorage.AddItem(newNonce, newUserAuthData);

//    return new NonceResult
//    {
//        ExpireMoment = newUserAuthData.GeneratedMoment.AddSeconds(10),
//        Nonce = newNonce,
//        Message = $"Please sign this message to authenticate with RZPrime: {newNonce}"
//    };
//}


///// <summary>
///// this method is for get jwt token
///// here check the nonce and wallet and signature with  nethereium
///// throw error if data is not valid
///// generate jwt token for valid data for login and update nonce storage
///// </summary>
///// <param name="update"></param>
///// <param name="ip"></param>
///// <returns></returns>
//public async Task<ActionResult> GetToken(NonceVerification update, string ip)
//{
//    ValidateClientInfo(update.ClientId, update.ClientSecret);

//    var userAuthData = ValidateNonce(update.Nonce, update.WalletAddress);

//    var message = $"Please sign this message to authenticate with RZPrime: {update.Nonce}";
//    VerifySignature(message, update.Signature, userAuthData.WalletAddress);

//    var user = await GetOrCreateUserAsync(userAuthData.WalletAddress);
//    //var user = await GetOrCreateUserAsync("test");
//    _userAuthStorage.RemoveItem(update.Nonce);

//    return Authenticate(user);
//}

//#region Private Methods
///// <summary>
///// Validates the provided nonce and associated wallet address.
///// </summary>
///// <param name="Nonce">The unique nonce value issued to the user for authentication.</param>
///// <param name="walletAddress">The wallet address provided by the client.</param>
///// <returns>The <see cref="UserAuthData"/> associated with the nonce if validation succeeds.</returns>
///// <exception cref="NonceNotFoundException">Thrown if the nonce does not exist in the storage.</exception>
///// <exception cref="BadRequestException">
///// Thrown if the nonce has expired or if the provided wallet address does not match the one stored for this nonce.
///// </exception>
///// <remarks>
///// This method ensures:
///// 1. The nonce exists in the <see cref="UserAuthStorage"/>.
///// 2. The nonce has not expired (valid for 10 seconds from creation).
///// 3. The provided wallet address matches the one originally associated with the nonce.
///// </remarks>
//private UserAuthData ValidateNonce(string Nonce, string walletAddress)
//{
//    var userAuthData = _userAuthStorage.GetItem(Nonce) ?? throw new NonceNotFoundException();

//    if (DateTime.UtcNow - userAuthData.GeneratedMoment > TimeSpan.FromSeconds(300))
//    {
//        _userAuthStorage.RemoveItem(Nonce);
//        throw new BadRequestException("nonce expired!");
//    }

//    if (!string.Equals(userAuthData.WalletAddress, walletAddress, StringComparison.OrdinalIgnoreCase))
//        throw new BadRequestException("Wallet address mismatch for nonce");

//    return userAuthData;
//}
