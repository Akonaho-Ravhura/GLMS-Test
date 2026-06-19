using FluentAssertions;
using GLMS_CORE_APP.Shared.Models;
using GLMS_CORE_APP.Shared.Models.Enums;
using GLMS_CORE_APP.Tests.Helpers;
using GLMS_CORE_APP.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace GLMS_CORE_APP.Tests.Auth;

public class UserTests : IDisposable
{
    private readonly GLMS_CORE_APP.Shared.Data.GlmsDbContext _db;

    public UserTests() => _db = DbContextFactory.Create();
    public void Dispose() => _db.Dispose();

    private static IList<ValidationResult> Validate(object model)
    {
        var ctx     = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public async Task User_CanBeSavedAndRetrieved()
    {
        var user = new User
        {
            FullName     = "Akonaho Ravhura",
            Email        = "akonaho@glms.co.za",
            PasswordHash = "hashed",
            Role         = Role.Admin
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var retrieved = await _db.Users.FindAsync(user.UserId);
        retrieved.Should().NotBeNull();
        retrieved!.FullName.Should().Be("Akonaho Ravhura");
        retrieved.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public async Task User_DefaultsToViewer_WhenRoleNotSet()
    {
        var user = new User { FullName = "Default", Email = "default@glms.co.za", PasswordHash = "h" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var retrieved = await _db.Users.FindAsync(user.UserId);
        retrieved!.Role.Should().Be(Role.Viewer);
    }

    [Fact]
    public async Task User_DefaultsToActive_OnCreation()
    {
        var user = new User { FullName = "Active", Email = "active@glms.co.za", PasswordHash = "h" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var retrieved = await _db.Users.FindAsync(user.UserId);
        retrieved!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task User_GeneratesGuid_OnCreation()
    {
        var user = new User { FullName = "Guid", Email = "guid@glms.co.za", PasswordHash = "h" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        user.UserId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task User_CreatedAt_IsSetToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var user = new User { FullName = "TS", Email = "ts@glms.co.za", PasswordHash = "h" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        user.CreatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task User_CanBeDeactivated()
    {
        var user = new User { FullName = "Deact", Email = "deact@glms.co.za", PasswordHash = "h", IsActive = true };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        user.IsActive  = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var retrieved = await _db.Users.FindAsync(user.UserId);
        retrieved!.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RegisterViewModel_IsValid_WithCorrectData()
    {
        var model = new RegisterViewModel
        {
            FullName = "Jane", Email = "jane@glms.co.za",
            Password = "Secure@Pass1", ConfirmPassword = "Secure@Pass1", Role = Role.Viewer
        };
        Validate(model).Should().BeEmpty();
    }

    [Fact]
    public void RegisterViewModel_FailsValidation_WhenEmailInvalid()
    {
        var model = new RegisterViewModel
        {
            FullName = "Jane", Email = "not-an-email",
            Password = "Secure@Pass1", ConfirmPassword = "Secure@Pass1", Role = Role.Viewer
        };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("Email"));
    }

    [Fact]
    public void RegisterViewModel_FailsValidation_WhenPasswordTooShort()
    {
        var model = new RegisterViewModel
        {
            FullName = "Jane", Email = "jane@glms.co.za",
            Password = "Ab1@", ConfirmPassword = "Ab1@", Role = Role.Viewer
        };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("Password"));
    }

    [Fact]
    public void RegisterViewModel_FailsValidation_WhenFullNameEmpty()
    {
        var model = new RegisterViewModel
        {
            FullName = "", Email = "jane@glms.co.za",
            Password = "Secure@Pass1", ConfirmPassword = "Secure@Pass1", Role = Role.Viewer
        };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("FullName"));
    }

    [Fact]
    public void LoginViewModel_FailsValidation_WhenEmailMissing()
    {
        var model = new LoginViewModel { Email = "", Password = "Secure@Pass1" };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("Email"));
    }

    [Fact]
    public void LoginViewModel_FailsValidation_WhenPasswordMissing()
    {
        var model = new LoginViewModel { Email = "jane@glms.co.za", Password = "" };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("Password"));
    }
}
