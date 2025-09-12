using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Utilities.Models.Storages
{
    public class ApplicationPoolStorage : Dictionary<string, ApplicationPool>, ISelfSingletonDependency
    {
    }

    public class ApplicationPool
    {
        public string PreSharedKey { get; set; }
        public string MasterSignature { get; set; }
    }
}
