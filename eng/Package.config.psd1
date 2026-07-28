@{
    SolutionPath = "JPSoftworks.CommandPalette.Extensions.Toolkit.slnx"
    PackagePropsPath = "eng\Package.props"
    PackageOutputPath = "artifacts\packages"
    LocalFeedPath = "artifacts\local-feed"

    PackageIds = @(
        "JPSoftworks.CommandPalette.Extensions.Toolkit"
        "JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions"
        "JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions"
        "JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog"
    )

    AotProjectPath = "test\JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest\JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest.csproj"
    AotProjectName = "JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest"
}
