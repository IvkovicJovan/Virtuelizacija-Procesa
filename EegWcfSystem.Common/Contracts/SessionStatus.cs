using System.Runtime.Serialization;

namespace EegWcfSystem.Common.Contracts
{
    [DataContract]
    public enum SessionStatus
    {
        [EnumMember] IN_PROGRESS = 0,
        [EnumMember] COMPLETED   = 1,
        [EnumMember] REJECTED    = 2
    }
}
