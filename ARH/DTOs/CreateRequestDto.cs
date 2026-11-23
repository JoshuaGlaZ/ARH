using ARH.Domain.Enums;

namespace ARH.DTOs
{

    public class CreateRequestDto
    {
        public string ClientRequestId { get; set; } = string.Empty;

        public Guid ApplicationId { get; set; }

        public EnvironmentType Environment { get; set; }

        public AccessLevel AccessLevel { get; set; }

        public string Justification { get; set; } = string.Empty;
    }
}