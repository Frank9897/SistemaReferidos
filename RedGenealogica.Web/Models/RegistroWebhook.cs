using System;

namespace RedGenealogica.Web.Models
{
    public class RegistroWebhook
    {
        public int Id { get; set; }

        public string IdPago { get; set; } = string.Empty;

        public string Estado { get; set; } = string.Empty;

        public DateTime FechaRegistro { get; set; }
    }
}