using System.ComponentModel.DataAnnotations;

namespace PmsZafiro.Application.DTOs.Products
{
    public class UpdateProductDto
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Range(0, int.MaxValue)]
        public int Stock { get; set; }

        public string Category { get; set; } = string.Empty;
        
        public string? ImageUrl { get; set; }
        
        public bool IsActive { get; set; }
        
        public bool IsStockTracked { get; set; }
    }
}