using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BoatSpotFinder.Web.Models;

public class BookingCreateViewModel : IValidatableObject
{
    public Guid SpotId { get; set; }
    public Guid MarinaId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? VesselId { get; set; }
    public BoatSpotFinder.Core.Models.PricePreview? Preview { get; set; }
    public List<SelectListItem> Vessels { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate <= StartDate)
        {
            yield return new ValidationResult(
                "Departure must be after the arrival date.",
                new[] { nameof(EndDate) });
        }
    }
}
