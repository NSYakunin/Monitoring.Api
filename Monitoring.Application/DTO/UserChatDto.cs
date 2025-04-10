using System;

namespace Monitoring.Application.DTO
{
    public class UserChatDto
    {
        public int IdUser { get; set; }

        public string Name { get; set; } = null!;

        public string? SmallName { get; set; }

        public int? IdDivision { get; set; }

        public string? Password { get; set; }

        public int? IdTypeUser { get; set; }

        public bool Isvalid { get; set; }
    }
}