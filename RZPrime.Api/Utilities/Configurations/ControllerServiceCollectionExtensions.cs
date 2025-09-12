using Microsoft.Extensions.Options;
using RZPrime.Schedulers;
using RZPrime.Services._BlockChain.DTOs.Settings;
using RZPrime.Services._BlockChainWebSocket.DTOs;
using RZPrime.Services._Encryption.DTOs.Settings;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._UserStage.DTOs.Settings;

namespace RZPrime.Api.Utilities.Configurations
{
    public static class ControllerServiceCollectionExtensions
    {
        public static void AddSettings(this IServiceCollection services, IConfiguration configuration)
        {            

            services.RegisterSetting<CustomEncryptionSetting>(configuration.GetSection(nameof(CustomEncryptionSetting)));         
            services.RegisterSetting<UserStageSetting>(configuration.GetSection(nameof(UserStageSetting)));         
            services.RegisterSetting<AvailableTokensSettings>(configuration.GetSection(nameof(AvailableTokensSettings)));         
            services.RegisterSetting<BlockchainWebSocketSetting>(configuration.GetSection(nameof(BlockchainWebSocketSetting)));         
            services.RegisterSetting<BlockChainSettings>(configuration.GetSection(nameof(BlockChainSettings)));         
            services.RegisterSetting<CallPriceSettings>(configuration.GetSection(nameof(CallPriceSettings)));         
        }

        private static void RegisterSetting<TSettings>(this IServiceCollection services, IConfigurationSection configuration)
           where TSettings : class, new()
        {
            services.Configure<TSettings>(configuration);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<TSettings>>().Value);
        }

        private static void RegisterSetting<TSettings, TISettings>(this IServiceCollection services, IConfigurationSection configuration)
            where TISettings : class
            where TSettings : class, TISettings, new()
        {
            services.Configure<TSettings>(configuration);
            services.AddSingleton<TISettings>(sp => sp.GetRequiredService<IOptions<TSettings>>().Value);
        }
    }
}
 