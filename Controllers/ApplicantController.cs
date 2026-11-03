using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SankalpAPI.Interface;
using SankalpAPI.Model;

namespace SankalpAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Consumes("application/json")]
    [AllowAnonymous]
    public class ApplicantController : ControllerBase
    {
        private readonly IApplicantService _applicantService;

        public ApplicantController(IApplicantService applicantService)
        {
            _applicantService = applicantService;
        }

        /// <summary>
        /// Accesses an external portal API (eLabharthi / Sankalp) passing portal_id (mapped as portType to external API),
        /// mobile_no, deviceid, session in JSON format, saves the applicant details into the database, and returns the beneficiary records.
        /// If the portal's URL is unavailable/unconfigured (e.g. portal_id = 5), initiates OTP-based applicant registration via SMS.
        /// </summary>
        /// <remarks>
        /// Sample Request (Portal URL available):
        /// 
        ///     POST /api/Applicant/GetApplicantDetails
        ///     {
        ///       "portal_id": 1,
        ///       "mobile_no": "9876543210",
        ///       "deviceid": "DEVICE123456",
        ///       "session": "2024-2025"
        ///     }
        /// 
        /// Sample Request (Portal URL unavailable - initiates OTP-based registration):
        /// 
        ///     POST /api/Applicant/GetApplicantDetails
        ///     {
        ///       "portal_id": 5,
        ///       "mobile_no": "9507637005",
        ///       "deviceid": "DEV123",
        ///       "session": "2026",
        ///       "UserType": "2"
        ///     }
        /// </remarks>
        [HttpPost("Applicant-Send-otp")]
        [ProducesResponseType(typeof(ApplicantListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status504GatewayTimeout)]
        public async Task<IActionResult> GetApplicantDetails([FromBody] ApplicantRequest request, CancellationToken cancellationToken)
        {
            var response = await _applicantService.GetApplicantDetailsAsync(request, cancellationToken);
            if (response.StatusCode == StatusCodes.Status400BadRequest)
            {
                return BadRequest(response);
            }

            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound(response);
            }

            if (response.StatusCode == StatusCodes.Status502BadGateway)
            {
                return StatusCode(StatusCodes.Status502BadGateway, response);
            }

            if (response.StatusCode == StatusCodes.Status504GatewayTimeout)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, response);
            }

            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Proxies the ApiByAvinash Sankalp full-details endpoint after OTP verification.
        /// Send the encrypted <c>data</c> body plus the <c>authtoken</c> and
        /// <c>authtoken1</c> headers returned by VerifyApplicantOtp.
        /// </summary>
        [HttpPost("GetApplicantDetails")]
        public async Task<IActionResult> GetApplicantFullDetails(
            [FromBody] SankalpApplicantLookupRequest request,
            [FromHeader(Name = "authtoken")] string? authToken,
            [FromHeader(Name = "authtoken1")] string? authToken1,
            CancellationToken cancellationToken)
        {
            var response = await _applicantService.GetApplicantFullDetailsAsync(
                request,
                authToken,
                authToken1,
                cancellationToken);
            CopyPortalHeaders(response);
            return PortalContent(response);
        }

        /// <summary>
        /// Verifies an OTP supplied with its request context.
        /// The external portal receives clientKey plus AES-encrypted otpId, otp, and mobileNo.
        /// </summary>
        /// <remarks>Request body: { "otpId": "29", "otp": "123456" }</remarks>
        [HttpPost("VerifyApplicantOtp")]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> VerifyApplicantOtp([FromBody] ApplicantOtpInput input, CancellationToken cancellationToken)
        {
            var request = new VerifyOtpRequest
            {
                Otp = input.Otp, OtpId = input.OtpId, ClientKey = input.ClientKey,
                PortalId = input.PortalId, MobileNo = input.MobileNo, OtpOnlyForward = true
            };
            var response = await _applicantService.VerifyOtpAsync(request, cancellationToken);
            CopyPortalHeaders(response);

            if (response.StatusCode == StatusCodes.Status400BadRequest)
            {
                return BadRequest(response);
            }

            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound(response);
            }

            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Proxies the external Sankalp applicant-list endpoint. Send the encrypted
        /// <c>data</c> value and forward the <c>authtoken</c> and <c>authtoken1</c>
        /// headers received from VerifyApplicantOtp.
        /// </summary>
        [HttpPost("GetApplicantList")]
        public async Task<IActionResult> GetApplicantList(
            [FromBody] SankalpApplicantLookupRequest request,
            [FromHeader(Name = "authtoken")] string? authToken,
            [FromHeader(Name = "authtoken1")] string? authToken1,
            CancellationToken cancellationToken)
        {
            var response = await _applicantService.GetApplicantListAsync(
                request,
                authToken,
                authToken1,
                cancellationToken);
            CopyPortalHeaders(response);
            return PortalContent(response);
        }

        private void CopyPortalHeaders(ApplicantResponse response)
        {
            foreach (var header in response.ResponseHeaders)
            {
                Response.Headers[header.Key] = header.Value;
            }
        }

        private static ContentResult PortalContent(ApplicantResponse response) => new()
        {
            Content = response.Data as string ?? string.Empty,
            ContentType = "application/json",
            StatusCode = response.StatusCode
        };


        /// <summary>
        /// Fetches beneficiary messages from the external portal API (eLabharthi http://10.133.20.151/eLabharthi/api/Beneficiary/Get_Beneficiary_Messages),
        /// saves each message into tbl_Message (including mobile number), and records the synced state in tbl_Message_log.
        /// </summary>
        /// <remarks>
        /// Sample Request:
        /// 
        ///     POST /api/Applicant/GetBeneficiaryMessages
        ///     {
        ///       "token": "408C6E3F4D04A7F7C1608193407356D8"
        ///     }
        /// </remarks>
        [HttpPost("GetBeneficiaryMessages")]
        [ProducesResponseType(typeof(GetBeneficiaryMessagesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status504GatewayTimeout)]
        public async Task<IActionResult> GetBeneficiaryMessages([FromBody] GetBeneficiaryMessagesRequest request, CancellationToken cancellationToken)
        {
            var response = await _applicantService.GetBeneficiaryMessagesAsync(request, cancellationToken);

            if (response.StatusCode == StatusCodes.Status400BadRequest)
            {
                return BadRequest(response);
            }

            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound(response);
            }

            if (response.StatusCode == StatusCodes.Status502BadGateway)
            {
                return StatusCode(StatusCodes.Status502BadGateway, response);
            }

            if (response.StatusCode == StatusCodes.Status504GatewayTimeout)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, response);
            }

            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Fetches pensioner/beneficiary status from the external portal API (eLabharthi http://10.133.20.151/eLabharthi/api/Beneficiary/Get_Beneficiary_Status)
        /// passing portalId, mobileNo, and beneficiaryNo.
        /// </summary>
        /// <remarks>
        /// Sample Request:
        /// 
        ///     POST /api/Applicant/GetBeneficiaryStatus
        ///     {
        ///       "portalId": 1,
        ///       "mobileNo": "9097372728",
        ///       "beneficiaryNo": "000000200577"
        ///     }
        /// </remarks>
        [HttpPost("GetBeneficiaryStatus")]
        [ProducesResponseType(typeof(BeneficiaryStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status504GatewayTimeout)]
        public async Task<IActionResult> GetBeneficiaryStatus([FromBody] GetBeneficiaryStatusRequest request, CancellationToken cancellationToken)
        {
            var response = await _applicantService.GetBeneficiaryStatusAsync(request, cancellationToken);

            if (response.StatusCode == StatusCodes.Status400BadRequest)
            {
                return BadRequest(response);
            }

            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound(response);
            }

            if (response.StatusCode == StatusCodes.Status502BadGateway)
            {
                return StatusCode(StatusCodes.Status502BadGateway, response);
            }

            if (response.StatusCode == StatusCodes.Status504GatewayTimeout)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, response);
            }

            return StatusCode(response.StatusCode, response);
        }
    }
}
