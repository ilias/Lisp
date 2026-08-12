param(
	[switch]$SkipContainer,
	[ValidatePattern("^[a-z0-9][a-z0-9._/-]*$")]
	[string]$ContainerImage = "lisp"
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
Push-Location $repoRoot

$publishTargets = @("win-x64", "linux-x64", "osx-x64", "osx-arm64")
$publishCommonArguments = @("/p:NoWarn=IL3000")

$pandocHeaderPath = Join-Path $env:TEMP "lisp-pandoc-dark-header.html"
$pandocLinkFilterPath = Join-Path $env:TEMP "lisp-pandoc-link-filter.lua"

try {
	if (-not (Get-Command pandoc -ErrorAction SilentlyContinue)) {
		throw "pandoc was not found in PATH. Install pandoc to enable Markdown to HTML conversion."
	}

	@"
<style>
html,
body {
	background: #0f1117;
	color: #e6edf3;
	font-family: "Segoe UI", "Noto Sans", Arial, sans-serif;
	line-height: 1.6;
}

main,
article,
body {
	max-width: 960px;
	margin: 0 auto;
	padding: 1.5rem;
}

a {
	color: #7cc7ff;
}

a:visited {
	color: #b494ff;
}

h1,
h2,
h3,
h4,
h5,
h6 {
	color: #f0f6fc;
}

code,
pre {
	font-family: Consolas, "Cascadia Code", monospace;
	background: #161b22;
	border: 1px solid #30363d;
	border-radius: 6px;
}

code {
	padding: 0.1rem 0.35rem;
}

pre {
	padding: 1rem;
	overflow-x: auto;
}

blockquote {
	border-left: 4px solid #3b82f6;
	margin: 1rem 0;
	padding: 0.25rem 1rem;
	color: #c9d1d9;
	background: #111827;
}

table {
	border-collapse: collapse;
	width: 100%;
}

th,
td {
	border: 1px solid #30363d;
	padding: 0.5rem 0.75rem;
}

th {
	background: #1f2937;
}
</style>
"@ | Set-Content -Path $pandocHeaderPath -Encoding UTF8

@"
local function has_scheme_or_anchor(target)
	return target:match("^[%a][%w+.-]*:") or target:match("^//") or target:match("^#")
end

function Link(el)
	if has_scheme_or_anchor(el.target) then
		return el
	end

	local path, suffix = el.target:match("^([^?#]+)(.*)$")
	if path and path:match("%.md$") then
		el.target = path:gsub("%.md$", ".html") .. suffix
	end

	return el
end
"@ | Set-Content -Path $pandocLinkFilterPath -Encoding UTF8

	foreach ($targetRid in $publishTargets) {
		dotnet publish -c Release "/p:PublishProfile=$targetRid" $publishCommonArguments

		$publishDir = Join-Path "publish" $targetRid
		$publishLibDir = Join-Path $publishDir "lib"
		$publishCliLibDir = Join-Path $publishDir "_cli_lib"
		$publishDocDir = Join-Path $publishDir "docs"

		foreach ($directory in @($publishLibDir, $publishCliLibDir, $publishDocDir)) {
			if (-not (Test-Path $directory)) {
				New-Item -ItemType Directory -Path $directory | Out-Null
			}
		}

		Copy-Item -Path "*.ss" -Destination $publishDir -Force
		Copy-Item -Path "*.md" -Destination $publishDir -Force
		Copy-Item -Path "lib\*.ss" -Destination $publishLibDir -Force
		Copy-Item -Path "_cli_lib\*.ss" -Destination $publishCliLibDir -Force
		Copy-Item -Path "docs\*" -Destination $publishDocDir -Recurse -Force

		$markdownFiles = Get-ChildItem -Path $publishDir -Filter "*.md" -Recurse -File
		foreach ($mdFile in $markdownFiles) {
			$htmlPath = [System.IO.Path]::ChangeExtension($mdFile.FullName, ".html")
			pandoc $mdFile.FullName --standalone --include-in-header=$pandocHeaderPath --lua-filter=$pandocLinkFilterPath --output $htmlPath
		}
	}

	if (-not $SkipContainer) {
		dotnet publish -c Release --os linux --arch x64 `
			"/t:PublishContainer" `
			"/p:PublishProfile=linux-x64" `
			$publishCommonArguments `
			"/p:PublishContainer=true" `
			"/p:ContainerRepository=$ContainerImage" `
			"/p:ContainerImageTags=latest" `
			"/p:LocalRegistry=Docker"
		Write-Host "Docker image generated: ${ContainerImage}:latest"
	}
}
finally {
	if (Test-Path $pandocHeaderPath) {
		Remove-Item -Path $pandocHeaderPath -Force
	}

	if (Test-Path $pandocLinkFilterPath) {
		Remove-Item -Path $pandocLinkFilterPath -Force
	}

	Pop-Location
}
