using GLMS_CORE_APP.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;

namespace GLMS_CORE_APP.Tests.Helpers;

public static class DbContextFactory
{
    public static GlmsDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<GlmsDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new GlmsDbContext(options);
    }
}

public static class SessionFactory
{
    public static ISession Create(
        string? userId   = null,
        string? userName = null,
        string? userRole = null)
    {
        var session = new Mock<ISession>();
        var store   = new Dictionary<string, byte[]>();

        session.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
               .Callback<string, byte[]>((k, v) => store[k] = v);

        session.Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]?>.IsAny))
               .Returns((string key, out byte[]? val) =>
               {
                   var found = store.TryGetValue(key, out var bytes);
                   val = bytes;
                   return found;
               });

        void SetStr(string key, string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(value));
            store[key] = bytes;
        }

        if (userId   != null) SetStr("UserId",   userId);
        if (userName != null) SetStr("UserName", userName);
        if (userRole != null) SetStr("UserRole", userRole);

        return session.Object;
    }
}

public static class HttpContextFactory
{
    public static HttpContext Create(ISession session)
    {
        var httpContext = new Mock<HttpContext>();
        httpContext.Setup(c => c.Session).Returns(session);
        return httpContext.Object;
    }
}
