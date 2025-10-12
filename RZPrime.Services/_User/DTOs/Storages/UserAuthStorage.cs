using System.Collections.Concurrent;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._User.DTOs.Storages
{
    public class UserAuthStorage : ConcurrentDictionary<string, UserAuthData>, ISelfSingletonDependency // key is nonce
    {
        private const int CleanupInterval = 60_000; // 1 minute for cleanup
        private readonly System.Timers.Timer _cleanupTimer;
        private readonly ConcurrentDictionary<string, object> _locks = new();
        //private readonly object _lockObject = new object();

        public void AddItem(string key, UserAuthData data)
        {
            this[key] = data;
        }

        public UserAuthStorage()
        {
            // Start a timer to remove old entries periodically
            _cleanupTimer = new System.Timers.Timer(CleanupInterval);
            _cleanupTimer.Elapsed += (sender, args) => {
                try { RemoveOldEntries(); }
                catch (Exception )
                {
                }
            };
            _cleanupTimer.Start();
        }

        public UserAuthData? GetItemByCode(string code)
        {
            foreach (var kvp in this.Values.ToList())
            {
                if (kvp.Code == code)
                {
                    return kvp;
                }
            }
            return null;
        }

        public bool VerifyNonce(string nonce, string deviceId,string walletAddress)
        {

            if (TryGetValue(nonce, out var data))
            {
                var lockObj = _locks.GetOrAdd(nonce, _ => new object());
                lock (lockObj)
                {
                    data.IsVerified = true;
                    data.DeviceId = deviceId;
                    data.WalletAddress = walletAddress;
                }
                return true;
            }
            return false;

            //if (TryGetValue(nonce, out var data))
            //{
            //    lock (data) 
            //    {
            //        data.IsVerified = true;
            //        data.DeviceId = deviceId;
            //        data.WalletAddress = walletAddress;
            //    }
            //    return true;
            //}
            //return false;
        }

        public UserAuthData? GetItem(string nonce)
        {
            if (TryGetValue(nonce, out var data))
            {
                return data;
            }
            return null;
        }

        public bool RemoveItem(string nonceId)
        {
            var removed = TryRemove(nonceId, out _);
            if (removed)
            {
                _locks.TryRemove(nonceId, out _);
            }
            return removed;
            //return TryRemove(nonceId, out _);
        }


        private void RemoveOldEntries()
        {
            var threshold = DateTime.UtcNow.AddSeconds(-310);
            var keysToRemove = this.Where(kv => kv.Value.GeneratedMoment < threshold)
                                  .Select(kv => kv.Key)
                                  .ToList();

            foreach (var key in keysToRemove)
            {
                if (TryRemove(key, out _))
                {
                    _locks.TryRemove(key, out _);
                }
            }

            //foreach (var kvp in this)
            //{
            //    if (kvp.Value.GeneratedMoment < threshold)
            //    {
            //        TryRemove(kvp.Key, out _);
            //    }
            //}
        }

        public void Dispose()
        {
            _cleanupTimer?.Stop();
            _cleanupTimer?.Dispose();
        }
    }


    public class UserAuthData
    {
        public DateTime GeneratedMoment { get; set; } = DateTime.UtcNow;
        public string Nonce { get; set; }
        public string Code { get; set; } 
        public string WalletAddress { get; set; }
        public string DeviceId { get; set; } 
        public bool IsVerified { get; set; } = false; 
        public string IP { get; set; }
    }


} 
