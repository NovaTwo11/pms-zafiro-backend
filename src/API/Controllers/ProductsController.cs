using Microsoft.AspNetCore.Mvc;
using PmsZafiro.Application.DTOs.Products;
using PmsZafiro.Application.Interfaces;
using PmsZafiro.Domain.Entities;

namespace PmsZafiro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductRepository _productRepository;

        public ProductsController(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
        {
            var products = await _productRepository.GetAllAsync();
            
            var productDtos = products.Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                UnitPrice = p.UnitPrice,
                Stock = p.Stock,
                Category = p.Category,
                ImageUrl = p.ImageUrl, 
                IsActive = p.IsActive,
                IsStockTracked = p.IsStockTracked,
                CreatedAt = p.CreatedAt
            });

            return Ok(productDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDto>> GetById(Guid id)
        {
            var p = await _productRepository.GetByIdAsync(id);
            if (p == null) return NotFound();

            var dto = new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                UnitPrice = p.UnitPrice,
                Stock = p.Stock,
                Category = p.Category,
                ImageUrl = p.ImageUrl, 
                IsActive = p.IsActive,
                IsStockTracked = p.IsStockTracked,
                CreatedAt = p.CreatedAt
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ProductDto>> Create(CreateProductDto createDto)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Description = createDto.Description,
                UnitPrice = createDto.UnitPrice,
                Stock = createDto.Stock,
                Category = createDto.Category,
                ImageUrl = createDto.ImageUrl,
                IsStockTracked = createDto.IsStockTracked
            };

            var createdProduct = await _productRepository.AddAsync(product);

            var responseDto = new ProductDto
            {
                Id = createdProduct.Id,
                Name = createdProduct.Name,
                Description = createdProduct.Description,
                UnitPrice = createdProduct.UnitPrice,
                Stock = createdProduct.Stock,
                Category = createdProduct.Category,
                ImageUrl = createdProduct.ImageUrl, // <--- AGREGADO
                IsActive = createdProduct.IsActive,
                CreatedAt = createdProduct.CreatedAt
            };

            return CreatedAtAction(nameof(GetById), new { id = createdProduct.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, UpdateProductDto updateDto)
        {
            if (id != updateDto.Id) return BadRequest("El ID no coincide.");

            var existingProduct = await _productRepository.GetByIdAsync(id);
            if (existingProduct == null) return NotFound();

            existingProduct.Name = updateDto.Name;
            existingProduct.Description = updateDto.Description;
            existingProduct.UnitPrice = updateDto.UnitPrice;
            existingProduct.Stock = updateDto.Stock;
            existingProduct.Category = updateDto.Category;
            existingProduct.ImageUrl = updateDto.ImageUrl;
            existingProduct.IsActive = updateDto.IsActive;
            existingProduct.IsStockTracked = updateDto.IsStockTracked;

            await _productRepository.UpdateAsync(existingProduct);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _productRepository.DeleteAsync(id);
            if (!result) return NotFound();

            return NoContent();
        }
    }
}