using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SankalpAPI.Interface;
using SankalpAPI.Model;

using SankalpAPI.Services;

namespace SankalpAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [AllowAnonymous]
    public class PortalController : ControllerBase
    {
        private readonly IPortalRepository _portalRepository;
        private readonly PortalService _portalService;
        private readonly IApplicantService _applicantService;

        public PortalController(
            IPortalRepository portalRepository,
            PortalService portalService,
            IApplicantService applicantService)
        {
            _portalRepository = portalRepository;
            _portalService = portalService;
            _applicantService = applicantService;
        }

        /// <summary>
        /// Step 1 in Mobile App Flow: Fetch list of available web applications/portals via POST JSON.
        /// </summary>
        [HttpPost("GetPortals")]
        [ProducesResponseType(typeof(PortalListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPortalsPost()
        {
            var portals = await _portalRepository.GetActivePortalsAsync();

            return Ok(new PortalListResponse
            {
                Status = true,
                Message = "Portals retrieved successfully.",
                Data = portals
            });
        }

        /// <summary>
        /// Step 1 in Mobile App Flow: Fetch list of available web applications/portals via GET.
        /// </summary>
        [HttpGet("GetPortals")]
        [ProducesResponseType(typeof(PortalListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPortals()
        {
            var portals = await _portalRepository.GetActivePortalsAsync();

            return Ok(new PortalListResponse
            {
                Status = true,
                Message = "Portals retrieved successfully.",
                Data = portals
            });
        }

        /// <summary>
        /// Fetch details of a specific portal by ID via POST JSON body.
        /// </summary>
        [HttpPost("GetPortal")]
        [Consumes("application/json")]
        public async Task<IActionResult> GetPortalPost([FromBody] PortalGetRequest request)
        {
            if (request == null || request.PortalId <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Valid PortalId is required."
                });
            }

            var portal = await _portalRepository.GetPortalByIdAsync(request.PortalId);
            if (portal == null)
            {
                return NotFound(new
                {
                    status = false,
                    message = "Portal not found."
                });
            }

            return Ok(new
            {
                status = true,
                data = portal
            });
        }

        [HttpPost("GetUserType")]
        [Consumes("application/json")]
        public async Task<IActionResult> GetUserTypePost([FromBody] PortalGetRequest request)
        {
            if (request == null || request.PortalId <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Valid PortalId is required."
                });
            }

            var portal = await _portalRepository.GetUserTypeByIdAsync(request.PortalId);
            if (portal == null)
            {
                return NotFound(new
                {
                    status = false,
                    message = "Portal not found."
                });
            }

            return Ok(new
            {
                status = true,
                data = portal
            });
        }

        /// <summary>
      
        [HttpPost("GetMapUserPortal")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(MapUserPortalResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMapUserPortal([FromBody] MapUserPortalRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Invalid request payload."
                });
            }

            var response = await _portalService.GetMapUserPortalAsync(request.PortalId, request.UserTypeId);

            if (!response.Status)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Connects to an external portal API resolved from mst_portal and injects credentials from tbl_map_user_portal.
        /// </summary>
        [HttpPost("Connect")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(PortalConnectResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(PortalConnectResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(PortalConnectResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(PortalConnectResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(PortalConnectResponse), StatusCodes.Status504GatewayTimeout)]
        public async Task<IActionResult> Connect([FromBody] PortalConnectRequest request, CancellationToken cancellationToken)
        {
            var response = await _portalService.ConnectAsync(request, cancellationToken);

            if (response.StatusCode == 404)
            {
                return NotFound(response);
            }

            if (response.StatusCode == 400)
            {
                return BadRequest(response);
            }

            if (response.StatusCode == 502)
            {
                return StatusCode(StatusCodes.Status502BadGateway, response);
            }

            if (response.StatusCode == 504)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, response);
            }

            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Accesses an external portal API passing mobile no, deviceid, session, portal id, and client key in JSON format,
        /// and returns details of the applicant.
        /// </summary>
        [HttpPost("GetApplicantDetails")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApplicantListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status504GatewayTimeout)]
        public async Task<IActionResult> GetApplicantDetails(
            [FromBody] ApplicantRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _applicantService.GetApplicantDetailsAsync(request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Verifies OTP via the external portal API (e.g. eLabharthi http://10.133.20.151/eLabharthiAPI/api/Beneficiary/verify-otp).
        /// Upon successful verification, generates and stores the TOTP secret key and client token in the database.
        /// </summary>
        /// <remarks>
        /// Sample Request:
        /// 
        ///     POST /api/Portal/VerifyOtp
        ///     {
        ///       "a_Id": [73, 74],
        ///       "mobileNo": "9097372728",
        ///       "otp": "558861",
        ///       "deviceId": "DEV123",
        ///       "session": "2025"
        ///     }
        /// </remarks>
        [HttpPost("VerifyOtp")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(VerifyOtpResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status504GatewayTimeout)]
        [ProducesResponseType(typeof(ApplicantResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> VerifyOtp(
            [FromBody] VerifyOtpRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _applicantService.VerifyOtpAsync(request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Fetch details of a specific portal by ID via GET URL parameter.
        /// </summary>
        //[HttpGet("GetPortal/{id}")]
        //public async Task<IActionResult> GetPortal(long id)
        //{
        //    var portal = await _portalRepository.GetPortalByIdAsync(id);
        //    if (portal == null)
        //    {
        //        return NotFound(new
        //        {
        //            status = false,
        //            message = "Portal not found."
        //        });
        //    }

        //    return Ok(new
        //    {
        //        status = true,
        //        data = portal
        //    });
        //}
    }
}
