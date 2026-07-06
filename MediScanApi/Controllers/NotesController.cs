using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediScanApi.Data;
using MediScanApi.Models;
using System.Security.Claims;

namespace MediScanApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotesController(AppDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetNotes([FromQuery] string? drugName)
        {
            var query = _context.DrugNotes.Where(n => n.UserId == CurrentUserId);

            if (!string.IsNullOrWhiteSpace(drugName))
            {
                // Use case-insensitive substring matching so queries like "tylenol"
                // will match stored names like "TYLENOL Extra Strength".
                var normalizedDrugName = drugName.Trim().ToLower();
                query = query.Where(n => n.DrugName.ToLower().Contains(normalizedDrugName));
            }

            var notes = await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(notes);
        }

        [HttpPost]
        public async Task<IActionResult> AddNote([FromBody] DrugNote note)
        {
            if (string.IsNullOrWhiteSpace(note.DrugName))
            {
                return BadRequest("Drug name is required.");
            }

            if (string.IsNullOrWhiteSpace(note.NoteText))
            {
                return BadRequest("Note text is required.");
            }

            note.UserId = CurrentUserId;
            note.DrugName = note.DrugName.Trim();
            note.NoteText = note.NoteText.Trim();

            _context.DrugNotes.Add(note);
            await _context.SaveChangesAsync();

            return Ok(note);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNote(int id, [FromBody] DrugNote updated)
        {
            var note = await _context.DrugNotes
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);

            if (note == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(updated.NoteText))
            {
                return BadRequest("Note text is required.");
            }

            note.NoteText = updated.NoteText.Trim();
            await _context.SaveChangesAsync();

            return Ok(note);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            var note = await _context.DrugNotes
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);

            if (note == null)
            {
                return NotFound();
            }

            _context.DrugNotes.Remove(note);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
