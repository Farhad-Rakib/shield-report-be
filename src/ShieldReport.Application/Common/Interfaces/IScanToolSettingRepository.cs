using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces;

public interface IScanToolSettingRepository : IRepository<ScanToolSetting>
{
    Task<ScanToolSetting?> GetByToolAsync(ScanTool tool, CancellationToken cancellationToken = default);
}
