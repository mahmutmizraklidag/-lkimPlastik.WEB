namespace ilkimPlastik.WEB.Models
{
    public class PaymentModel
    {
        public string CardNumber { get; set; }

        public string CardHolderName { get; set; }

        public string ExpirationDate { get; set; }

        public string CVV { get; set; }

        public string Name { get; set; }

        public string Surname { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string IdentificationNumber { get; set; }

        public string City { get; set; }

        public string District { get; set; }

        public string Address { get; set; }
        public string? PostCode { get; set; }
        public int? SelectedAddressId { get; set; }
    }
}
