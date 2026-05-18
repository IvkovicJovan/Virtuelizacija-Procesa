using System.Runtime.Serialization;

namespace EegWcfSystem.Common.Faults
{
    [DataContract]
    public class DataFormatFault
    {
        [DataMember] public string Reason   { get; set; }
        [DataMember] public string Field    { get; set; }
        [DataMember] public int    RowIndex { get; set; }
    }

    [DataContract]
    public class ValidationFault
    {
        [DataMember] public string Reason   { get; set; }
        [DataMember] public string Rule     { get; set; }
        [DataMember] public int    RowIndex { get; set; }
    }
}
