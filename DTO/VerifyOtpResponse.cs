using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SankalpAPI.DTO
{
    public class VerifyOtpResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("userExists")]
        public bool UserExists { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public UserData? Data { get; set; }
    }

    public class UserData
    {
        [JsonPropertyName("userId")]
        public long UserId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mobileNo")]
        public string MobileNo { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Email { get; set; }

        [JsonPropertyName("distCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DistCode { get; set; }

        [JsonPropertyName("blockCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BlockCode { get; set; }

        [JsonPropertyName("panchayatCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? PanchayatCode { get; set; }

        [JsonPropertyName("areaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AreaType { get; set; }

        [JsonPropertyName("villCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? VILLCode { get; set; }

        [JsonPropertyName("wardCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WARDCODE { get; set; }

        [JsonPropertyName("genderCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? GenderCode { get; set; }

        [JsonPropertyName("ageGroupId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? AgeGroupId { get; set; }

        [JsonPropertyName("categoryId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CategoryId { get; set; }

        [JsonPropertyName("qualificationId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? QualificationId { get; set; }

        [JsonPropertyName("occupationId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? OccupationId { get; set; }

        [JsonPropertyName("deviceId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeviceId { get; set; }

        [JsonPropertyName("token")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Token { get; set; }

        [JsonPropertyName("entryDate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? EntryDate { get; set; }

        [JsonPropertyName("DistName")]
        public string DistName { get; set; } = string.Empty;

        [JsonPropertyName("BlockName")]
        public string BlockName { get; set; } = string.Empty;

        [JsonPropertyName("PanchayatName")]
        public string PanchayatName { get; set; } = string.Empty;

        [JsonPropertyName("VILLNAME")]
        public string VILLNAME { get; set; } = string.Empty;

        [JsonPropertyName("WARDNAME")]
        public string WARDNAME { get; set; } = string.Empty;

        [JsonPropertyName("genderName")]
        public string genderName { get; set; } = string.Empty;

        [JsonPropertyName("ageGroupName")]
        public string ageGroupName { get; set; } = string.Empty;

        [JsonPropertyName("categoryName")]
        public string categoryName { get; set; } = string.Empty;

        [JsonPropertyName("qualificationName")]
        public string qualificationName { get; set; } = string.Empty;

        [JsonPropertyName("occupationName")]
        public string occupationName { get; set; } = string.Empty;

        // Multiple interests from MapUser_Interest (IDs)
        [JsonPropertyName("interests")]
        public List<long> Interests { get; set; } = new List<long>();

        // Detailed interest items from MapUser_Interest & mst_interest
        [JsonPropertyName("userInterests")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<UserInterestData>? UserInterests { get; set; }
    }

    public class UserInterestData
    {
        [JsonPropertyName("mapId")]
        public long MapId { get; set; }

        [JsonPropertyName("interestId")]
        public long InterestId { get; set; }

        [JsonPropertyName("interestName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? InterestName { get; set; }
    }
}
