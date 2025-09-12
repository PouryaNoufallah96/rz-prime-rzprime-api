//using RZPrime.Services._Price.DTOs.Results;
//using System.Collections.Concurrent;
//using static _CodeAssistant.Constants.RegisterMode;

//namespace RZPrime.Services._Price.DTOs.Storages
//{
//    public class PriceDataStorage : ConcurrentDictionary<string, PriceResult>, ISelfSingletonDependency
//    {
//        public void AddOrUpdatePrice(string key, PriceResult newValue)
//        {
//            this.AddOrUpdate(
//                key,
//                newValue, 
//                (existingKey, existingValue) => newValue 
//            );
//        }


//    }
//}
