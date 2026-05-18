using System.ServiceModel;
using EegWcfSystem.Common.Faults;

namespace EegWcfSystem.Common.Contracts
{
    [ServiceContract]
    public interface IEegService
    {
        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        AckResponse StartSession(EegMeta meta);

        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract(typeof(ValidationFault))]
        AckResponse PushSample(EegSample sample);

        [OperationContract]
        AckResponse EndSession();
    }
}
