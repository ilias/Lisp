$ErrorActionPreference = "Stop"

$profiles = @("win-x64", "linux-x64", "osx-x64", "osx-arm64")

foreach ($profile in $profiles) {
	dotnet publish -c Release "/p:PublishProfile=$profile"

	$publishDir = Join-Path "publish" $profile
	$publishLibDir = Join-Path $publishDir "lib"
	$publishDocDir = Join-Path $publishDir "docs"

	if (-not (Test-Path $publishLibDir)) {
		New-Item -ItemType Directory -Path $publishLibDir | Out-Null
	}

	if (-not (Test-Path $publishDocDir)) {
		New-Item -ItemType Directory -Path $publishDocDir | Out-Null
	}

	Copy-Item -Path "*.ss" -Destination $publishDir -Force
	Copy-Item -Path "*.md" -Destination $publishDir -Force
	Copy-Item -Path "*.dll" -Destination $publishDir -Force
	Copy-Item -Path "lib\*.ss" -Destination $publishLibDir -Force
	Copy-Item -Path "docs\*" -Destination $publishDocDir -Recurse -Force
}
