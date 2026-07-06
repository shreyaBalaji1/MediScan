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
    public class FavoritesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FavoritesController(AppDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetFavorites()
        {
            var favorites = await _context.FavoriteDrugs
                .Where(f => f.UserId == CurrentUserId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return Ok(favorites);
        }

        [HttpPost]
        public async Task<IActionResult> AddFavorite([FromBody] FavoriteDrug favorite)
        {
            if (string.IsNullOrWhiteSpace(favorite.DrugName))
            {
                return BadRequest("Drug name is required.");
            }

            var userId = CurrentUserId;
            var normalizedDrugName = favorite.DrugName.Trim().ToLower();

            var existingFavorite = await _context.FavoriteDrugs
                .FirstOrDefaultAsync(f => f.UserId == userId && f.DrugName.ToLower() == normalizedDrugName);

            if (existingFavorite != null)
            {
                return Conflict("This drug is already in favorites.");
            }

            favorite.UserId = userId;
            favorite.DrugName = favorite.DrugName.Trim();

            _context.FavoriteDrugs.Add(favorite);
            await _context.SaveChangesAsync();

            return Ok(favorite);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFavorite(int id)
        {
            var favorite = await _context.FavoriteDrugs
                .FirstOrDefaultAsync(f => f.Id == id && f.UserId == CurrentUserId);

            if (favorite == null)
            {
                return NotFound();
            }

            _context.FavoriteDrugs.Remove(favorite);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
