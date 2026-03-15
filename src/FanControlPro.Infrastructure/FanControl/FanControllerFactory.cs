using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using Microsoft.Extensions.Logging;

namespace FanControlPro.Infrastructure.FanControl;

public sealed class FanControllerFactory : IFanControllerFactory
{
    private readonly AsusEcControllerV2 _asus;
    private readonly GigabyteEcControllerV2 _gigabyte;
    private readonly MsiEcControllerV2 _msi;
    private readonly MonitoringOnlyController _monitoringOnly;
    private readonly IControlOnboardingService _onboardingService;
    private readonly IWriteCapabilityValidator _validator;
    private readonly ILoggerFactory _loggerFactory;

    public FanControllerFactory(
        AsusEcControllerV2 asus,
        GigabyteEcControllerV2 gigabyte,
        MsiEcControllerV2 msi,
        MonitoringOnlyController monitoringOnly,
        IControlOnboardingService onboardingService,
        IWriteCapabilityValidator validator,
        ILoggerFactory loggerFactory)
    {
        _asus = asus;
        _gigabyte = gigabyte;
        _msi = msi;
        _monitoringOnly = monitoringOnly;
        _onboardingService = onboardingService;
        _validator = validator;
        _loggerFactory = loggerFactory;
    }

    public IFanControllerV2 Create(FanChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var primary = ResolveVendorController(channel.Vendor);
        var resilient = new ResilientFanController(
            primary,
            _monitoringOnly,
            _loggerFactory.CreateLogger<ResilientFanController>());

        return new GuardedFanController(resilient, _onboardingService, _validator);
    }

    private IFanControllerV2 ResolveVendorController(string? vendor)
    {
        if (string.Equals(vendor, "ASUS", StringComparison.OrdinalIgnoreCase))
        {
            return _asus;
        }

        if (string.Equals(vendor, "Gigabyte", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(vendor, "AORUS", StringComparison.OrdinalIgnoreCase))
        {
            return _gigabyte;
        }

        if (string.Equals(vendor, "MSI", StringComparison.OrdinalIgnoreCase))
        {
            return _msi;
        }

        return _monitoringOnly;
    }
}
