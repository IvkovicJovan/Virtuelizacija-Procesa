using System;
using System.Runtime.Serialization;

namespace EegWcfSystem.Common.Contracts
{
    [DataContract]
    public class EegSample
    {
        [DataMember] public int      RowIndex       { get; set; }
        [DataMember] public DateTime Timestamp      { get; set; }

        // EEG kanali
        [DataMember] public double AF3 { get; set; }
        [DataMember] public double T7  { get; set; }
        [DataMember] public double Pz  { get; set; }
        [DataMember] public double T8  { get; set; }
        [DataMember] public double AF4 { get; set; }

        // Kognitivne metrike
        [DataMember] public double Attention   { get; set; }
        [DataMember] public double Engagement  { get; set; }
        [DataMember] public double Excitement  { get; set; }
        [DataMember] public double Interest    { get; set; }
        [DataMember] public double Relaxation  { get; set; }
        [DataMember] public double Stress      { get; set; }

        // Metapodaci
        [DataMember] public int Battery        { get; set; }
        [DataMember] public int ContactQuality { get; set; }
        [DataMember] public int SlideIndex     { get; set; }
        [DataMember] public int SetIndex       { get; set; }
    }
}
