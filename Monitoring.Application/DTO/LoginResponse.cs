using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    /// <summary>
    /// DTO-модель ответа при логине (содержит JWT)
    /// </summary>
    public class LoginResponse
    {
        public string Token { get; set; } = "";
        public string UserName { get; set; } = "";
        public int? DivisionId { get; set; }
    }
}
