using System.Runtime.Serialization;

namespace EegWcfSystem.Common.Contracts
{
    [DataContract]
    public class EegMeta
    {
        [DataMember] public int    ParticipantId { get; set; }
        [DataMember] public string FileName      { get; set; }
        [DataMember] public int    TotalRows     { get; set; }
        [DataMember] public string SchemaVersion { get; set; }
    }
}
