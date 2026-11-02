using System.Threading;
using System.Threading.Tasks;
using SankalpAPI.Model;

namespace SankalpAPI.Interface
{
    public interface IApplicantService
    {
        /// <summary>
        /// Communicates with an external portal API to fetch applicant details
        /// based on mobile_no, deviceid, session, portal_id, and client_key.
        /// </summary>
        Task<ApplicantResponse> GetApplicantDetailsAsync(ApplicantRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies OTP and, upon successful verification, generates and stores
        /// the TOTP secret key and client token in the database.
        /// </summary>
        Task<ApplicantResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

        /// <summary>Forwards the encrypted Sankalp applicant-list request with its OTP authentication headers.</summary>
        Task<ApplicantResponse> GetApplicantListAsync(SankalpApplicantLookupRequest request, string? authToken, string? authToken1, CancellationToken cancellationToken = default);

        /// <summary>Forwards the encrypted Sankalp full-details request with its OTP authentication headers.</summary>
        Task<ApplicantResponse> GetApplicantFullDetailsAsync(SankalpApplicantLookupRequest request, string? authToken, string? authToken1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Communicates with an external portal API (eLabharthi /api/Beneficiary/Get_Beneficiary_Messages)
        /// to fetch beneficiary messages using the user's token, saves each message into tbl_Message (with mobile),
        /// and records the synced status in tbl_Message_log.
        /// </summary>
        Task<ApplicantResponse> GetBeneficiaryMessagesAsync(GetBeneficiaryMessagesRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Communicates with external portal API (eLabharthi /api/Beneficiary/Get_Beneficiary_Status)
        /// to fetch pensioner/beneficiary status based on portalId, mobileNo, and beneficiaryNo.
        /// </summary>
        Task<ApplicantResponse> GetBeneficiaryStatusAsync(GetBeneficiaryStatusRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies the server-generated OTP for portals where an external portal URL is unavailable,
        /// and inserts the applicant's portal mapping into tbl_map_user_portal.
        /// </summary>
        Task<ApplicantResponse> VerifyApplicantOtpAsync(VerifyApplicantOtpRequest request, CancellationToken cancellationToken = default);
    }
}
