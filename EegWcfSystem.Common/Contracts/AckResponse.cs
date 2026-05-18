using System.Runtime.Serialization;

namespace EegWcfSystem.Common.Contracts
{
    [DataContract]
    public class AckResponse
    {
        [DataMember] public bool          Success      { get; set; }
        [DataMember] public SessionStatus Status       { get; set; }
        [DataMember] public string        Message      { get; set; }
        [DataMember] public int           LastRowIndex { get; set; }
    }
}
