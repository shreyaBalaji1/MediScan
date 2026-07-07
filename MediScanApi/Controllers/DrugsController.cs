using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.RegularExpressions;

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
                    warnings = result.Info.Warnings,
                    dosageAndAdministration = result.Info.DosageAndAdministration,
                    pediatricUse = result.Info.PediatricUse,
                    geriatricUse = result.Info.GeriatricUse
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
            var excerptsA = ExtractMatchingExcerpts(infoA, drugB);
            var excerptsB = ExtractMatchingExcerpts(infoB, drugA);

            return Ok(new
            {
                drugA = new
                {
                    name = infoA.Name,
                    mentionsOther = excerptsA.Count > 0,
                    matchedExcerpts = excerptsA,
                    fullText = infoA.DrugInteractions
                },
                drugB = new
                {
                    name = infoB.Name,
                    mentionsOther = excerptsB.Count > 0,
                    matchedExcerpts = excerptsB,
                    fullText = infoB.DrugInteractions
                },
                possibleInteractionFlagged = excerptsA.Count > 0 || excerptsB.Count > 0,
                disclaimer = "This checks whether each drug's FDA label text mentions the other drug by name. " +
                             "It is a simple keyword match, not a clinical interaction database — always consult " +
                             "a pharmacist or doctor before combining medications."
            });
        }

        private enum LabelFetchStatus { Ok, NotFound, UpstreamError }

        private record DrugLabelInfo(
            string Name,
            string Purpose,
            string Warnings,
            string DrugInteractions,
            string DosageAndAdministration,
            string PediatricUse,
            string GeriatricUse);

        private record LabelFetchResult(LabelFetchStatus Status, DrugLabelInfo? Info, string? ErrorMessage);

        // FDA label text is one giant blob per section, which isn't useful to read as-is.
        // Instead of dumping the whole thing, split it into sentences and surface only the
        // ones that actually mention the other drug, so the UI can show a short, targeted
        // excerpt instead of a wall of text.
        private static List<string> ExtractMatchingExcerpts(DrugLabelInfo info, string otherDrugName, int maxExcerpts = 4)
        {
            var needle = otherDrugName.Trim();
            var matches = new List<string>();
            if (needle.Length == 0) return matches;

            var haystack = $"{info.Warnings} {info.DrugInteractions}";

            // Split on sentence boundaries, and also before "Table N:" and numbered subsection
            // headers (e.g. "7.4 Antibiotics...") — FDA label text collapses tables into
            // unpunctuated runs of text, so without these extra breakpoints a single "sentence"
            // can end up being an entire table.
            var sentences = Regex.Split(
                haystack.Trim(),
                @"(?<=[.!?])\s+(?=[A-Z0-9])|\s+(?=Table\s+\d+:)|\s+(?=\d\.\d\s+[A-Z][a-z])");

            foreach (var raw in sentences)
            {
                var sentence = raw.Trim();
                // Skip short fragments (section numbers, table headers) that aren't useful on their own.
                if (sentence.Length < 20) continue;
                if (!sentence.Contains(needle, StringComparison.OrdinalIgnoreCase)) continue;

                if (sentence.Length > 400)
                {
                    var cutoff = sentence.LastIndexOf(' ', 380);
                    sentence = sentence[..(cutoff > 0 ? cutoff : 380)] + "…";
                }

                if (!matches.Contains(sentence))
                {
                    matches.Add(sentence);
                }

                if (matches.Count >= maxExcerpts) break;
            }

            return matches;
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
            if (result.TryGetProperty("openfda", out var openfda) &&
                openfda.TryGetProperty("brand_name", out var brandNames) &&
                brandNames.GetArrayLength() > 0)
            {
                drugName = brandNames[0].GetString() ?? name;
            }

            var info = new DrugLabelInfo(
                Name: drugName,
                Purpose: GetLabelField(result, "purpose", "No purpose available."),
                Warnings: GetLabelField(result, "warnings", "No warnings available."),
                DrugInteractions: GetLabelField(result, "drug_interactions", "No drug interaction information available."),
                DosageAndAdministration: GetLabelField(result, "dosage_and_administration", "No dosage information available."),
                PediatricUse: GetLabelField(result, "pediatric_use", "No pediatric-specific information available."),
                GeriatricUse: GetLabelField(result, "geriatric_use", "No geriatric-specific information available."));

            return new LabelFetchResult(LabelFetchStatus.Ok, info, null);
        }

        // openFDA label sections are each a one-element string array when present.
        private static string GetLabelField(JsonElement result, string propertyName, string fallback)
        {
            if (result.TryGetProperty(propertyName, out var array) && array.GetArrayLength() > 0)
            {
                return array[0].GetString() ?? fallback;
            }

            return fallback;
        }
    }
}
