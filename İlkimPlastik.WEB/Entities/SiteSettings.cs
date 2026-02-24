namespace ilkimPlastik.WEB.Entities
{
    public class SiteSettings
    {
        public int Id { get; set; } // genelde tek kayıt tutulur (Id=1)

        public string? LogoFileName { get; set; }
        public string? FaviconFileName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? AddressText { get; set; }

        // Basit SEO alanları
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Keywords { get; set; }
        public string? Author { get; set; }



        //sosyal medya linkleri
        public string? FacebookUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? LinkedInUrl { get; set; }


        //çalışma saatleri
        public string? WorkingHours { get; set; }


        //iyzico 
        public string? ApiUrl{ get; set; }
        public string? IyzicoApiKey { get; set; }
        public string? IyzicoSecretKey { get; set; }
        public string? CallBackUrl { get; set; }  


        // bilgilendirme alacak olan işletme mail hesabı
        public string? NotificationEmail { get; set; }

        //hakkımızda sayfası
        public string? AboutText { get; set; }

        //politikalar, gizlilik sözleşmesi gibi ek alanlar eklenebilir
        public string? KvkkPdfName { get; set; } // kişisel verilerin korunması kanunu
        public string? PrivacyPolicyPdfName { get; set; } // gizlilik politikası
        public string? ConditionsPdfName { get; set; } // şartlar & koşullar
        public string? ReturnPolicyPdfName { get; set; } // iade politikası
        public string? DistanceSalesAgreementPdfName { get; set; } // mesafeli satış sözleşmesi



    }
}
