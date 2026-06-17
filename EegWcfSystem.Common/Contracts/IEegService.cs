using System.ServiceModel;
using EegWcfSystem.Common.Faults;

namespace EegWcfSystem.Common.Contracts
{
    // WCF ugovor za protokol iz PDF-a:
    // StartSession(meta) -> PushSample(sample) -> EndSession().
    // SessionMode.Allowed dozvoljava netTcp sesiju, ali servis ne zavisi od krhkog WCF stanja
    // jer je serverska implementacija Singleton i sama čuva aktivnu sesiju.
    [ServiceContract(SessionMode = SessionMode.Allowed)]
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
        [FaultContract(typeof(ValidationFault))]
        AckResponse EndSession();
    }
}
