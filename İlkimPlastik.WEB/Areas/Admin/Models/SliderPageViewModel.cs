using System.ComponentModel.DataAnnotations;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Http;

namespace ilkimPlastik.WEB.Areas.Admin.Models
{
    public class SliderPageViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Başlık zorunludur.")]
        [Display(Name = "Başlık")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Açıklama zorunludur.")]
        [Display(Name = "Açıklama")]
        public string Description { get; set; }

        [Display(Name = "Seo Url")]
        public string? SefUrl { get; set; }

        [Display(Name = "Görsel")]
        public IFormFile? ImageFile { get; set; }

        public string? ExistingImage { get; set; }

        public List<Slider> Sliders { get; set; } = new();

        public bool IsEditMode => Id.HasValue;
    }
}