
using ARH.Domain.Entities;

namespace ARH.DTOs
{
    public sealed record CreateRequestResult(
        AccessRequest Request,
        bool IsReplay
    );
}