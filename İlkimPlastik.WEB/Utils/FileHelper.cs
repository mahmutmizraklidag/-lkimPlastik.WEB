namespace ilkimPlastik.WEB.Utils
{
    public class FileHelper
    {
        public static async Task<string> FileLoaderAsync(IFormFile formFile, string filePath = "/wwwroot/img/")
        {
            if (formFile == null || formFile.Length == 0)
                return null;

            // Orijinal dosya adı
            string originalName = Path.GetFileNameWithoutExtension(formFile.FileName);
            string extension = Path.GetExtension(formFile.FileName);

            // Yeni isim (GUID + orijinal isim)
            string newFileName = $"{Guid.NewGuid()}_{originalName}{extension}";

            // Kayıt yapılacak klasör
            string directoryPath = Directory.GetCurrentDirectory() + filePath;

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            // Tam dosya yolu
            string fileFullPath = Path.Combine(directoryPath, newFileName);

            using var stream = new FileStream(fileFullPath, FileMode.Create);
            await formFile.CopyToAsync(stream);

            return newFileName;
        }


        // Fiziksel dosya silme metodu
        public static void DeleteFile(string fileName, string filePath = "/wwwroot/img/")
        {
            if (string.IsNullOrEmpty(fileName)) return;

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath.TrimStart('/'), fileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public static async Task<string> UploadCvAsync(IFormFile file, IWebHostEnvironment env)
        {
            if (file == null || file.Length == 0)
                return null;

            var ext = Path.GetExtension(file.FileName).ToLower();
            var allowed = new[] { ".pdf", ".doc", ".docx" };

            if (!allowed.Contains(ext))
                return "invalid";

            var folder = Path.Combine(env.WebRootPath, "uploads/cv");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = Guid.NewGuid() + ext;
            var path = Path.Combine(folder, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/uploads/cv/" + fileName;
        }
    }
}
