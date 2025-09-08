using Application.Contracts;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Identity.TenantProvider
{
    class HttpContextTenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _http;

        public HttpContextTenantProvider(IHttpContextAccessor http) => _http = http;

        public Guid TenantId
        {
            get
            {
                var claim = _http.HttpContext?.User?.FindFirst("tid")?.Value;
                if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var t))
                    throw new UnauthorizedAccessException("Tenant claim missing or invalid");
                return t;
            }
        }
    }
}
