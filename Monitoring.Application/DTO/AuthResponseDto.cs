using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    /// <summary>
    /// Ответ при успешной аутентификации
    /// (содержит сам токен и, возможно, дополнительную информацию)
    /// </summary>
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
        public string UserName { get; set; } = string.Empty;

        // Например, если хотите возвращать divisionId
        public int DivisionId { get; set; }
    }
}
