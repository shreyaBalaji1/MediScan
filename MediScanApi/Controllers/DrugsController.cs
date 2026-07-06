using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MediScanApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DrugsController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public DrugsController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchDrug([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Drug name is required.");
            }

            var result = await FetchDrugLabelAsync(name);

            return result.Status switch
            {
                LabelFetchStatus.Ok => Ok(new
                {
                    name = result.Info!.Name,
                    purpose = result.Info.Purpose,
                    warnings = result.Info.Warnings
                }),
                LabelFetchStatus.NotFound => NotFound("Drug not found."),
                _ => StatusCode(502, result.ErrorMessage)
            };
        }

        [HttpGet("interactions")]
        public async Task<IActionResult> CheckInteraction([FromQuery] string drugA, [FromQuery] string drugB)
        {
            if (string.IsNullOrWhiteSpace(drugA) || string.IsNullOrWhiteSpace(drugB))
            {
                return BadRequest("Both drug names are required.");
            }

            var resultA = await FetchDrugLabelAsync(drugA);
            if (resultA.Status != LabelFetchStatus.Ok)
            {
                return resultA.Status == LabelFetchStatus.NotFound
                    ? NotFound($"'{drugA}' not found.")
                    : StatusCode(502, resultA.ErrorMessage);
            }

            var resultB = await FetchDrugLabelAsync(drugB);
            if (resultB.Status != LabelFetchStatus.Ok)
            {
                return resultB.Status == LabelFetchStatus.NotFound
                    ? NotFound($"'{drugB}' not found.")
                    : StatusCode(502, resultB.ErrorMessage);
            }

            var infoA = resultA.Info!;
            var infoB = resultB.Info!;

            // Cross-check using the names the user actually typed (e.g. "aspirin"), since a
            // label's interaction text tends to reference generic/common names rather than
            // another label's specific brand name (e.g. "Low Dose Aspirin").
            var aMentionsB = MentionsDrug(infoA, drugB);
            var bMentionsA = MentionsDrug(infoB, drugA);

            return Ok(new
            {
                drugA = new { name = infoA.Name, drugInteractions = infoA.DrugInteractions },
                drugB = new { name = infoB.Name, drugInteractions = infoB.DrugInteractions },
                possibleInteractionFlagged = aMentionsB || bMentionsA,
                disclaimer = "This checks whether each drug's FDA label text mentions the other drug by name. " +
                             "It is a simple keyword match, not a clinical interaction database — always consult " +
                             "a pharmacist or doctor before combining medications."
            });
        }

        private enum LabelFetchStatus { Ok, NotFound, UpstreamError }

        private record DrugLabelInfo(string Name, string Purpose, string Warnings, string DrugInteractions);

        private record LabelFetchResult(LabelFetchStatus Status, DrugLabelInfo? Info, string? ErrorMessage);

        private static bool MentionsDrug(DrugLabelInfo info, string otherDrugName)
        {
            var needle = otherDrugName.Trim();
            if (needle.Length == 0) return false;

            var haystack = $"{info.Warnings} {info.DrugInteractions}";
            return haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<LabelFetchResult> FetchDrugLabelAsync(string name)
        {
            var encodedName = Uri.EscapeDataString(name.Trim());
            var url = $"https://api.fda.gov/drug/label.json?search=openfda.brand_name:\"{encodedName}\"&limit=1";

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(url);
            }
            catch (HttpRequestException)
            {
                return new LabelFetchResult(LabelFetchStatus.UpstreamError, null,
                    "Could not reach the drug information service.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new LabelFetchResult(LabelFetchStatus.NotFound, null, null);
            }

            var content = await response.Content.ReadAsStringAsync();

            JsonDocument jsonDoc;
            try
            {
                jsonDoc = JsonDocument.Parse(content);
            }
            catch (JsonException)
            {
                return new LabelFetchResult(LabelFetchStatus.UpstreamError, null,
                    "Received an invalid response from the drug information service.");
            }

            using var _ = jsonDoc;

            if (!jsonDoc.RootElement.TryGetProperty("results", out var results) ||
                results.GetArrayLength() == 0)
            {
                return new LabelFetchResult(LabelFetchStatus.NotFound, null, null);
            }

            var result = results[0];

            string drugName = name;
            string warnings = "No warnings available.";
            string purpose = "No purpose available.";
            string interactions = "No drug interaction information available.";

            if (result.TryGetProperty("openfda", out var openfda) &&
                openfda.TryGetProperty("brand_name", out var brandNames) &&
                brandNames.GetArrayLength() > 0)
            {
                drugName = brandNames[0].GetString() ?? name;
            }

            if (result.TryGetProperty("warnings", out var warningArray) &&
                warningArray.GetArrayLength() > 0)
            {
                warnings = warningArray[0].GetString() ?? warnings;
            }

            if (result.TryGetProperty("purpose", out var purposeArray) &&
                purposeArray.GetArrayLength() > 0)
            {
                purpose = purposeArray[0].GetString() ?? purpose;
            }

            if (result.TryGetProperty("drug_interactions", out var interactionArray) &&
                interactionArray.GetArrayLength() > 0)
            {
                interactions = interactionArray[0].GetString() ?? interactions;
            }

            return new LabelFetchResult(LabelFetchStatus.Ok,
                new DrugLabelInfo(drugName, purpose, warnings, interactions), null);
        }
    }
}
