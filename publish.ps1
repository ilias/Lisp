$ErrorActionPreference = "Stop"

$publishTargets = @("win-x64", "linux-x64", "osx-x64", "osx-arm64")

if (-not (Get-Command pandoc -ErrorAction SilentlyContinue)) {
	throw "pandoc was not found in PATH. Install pandoc to enable Markdown to HTML conversion."
}

$pandocHeaderPath = Join-Path $env:TEMP "lisp-pandoc-dark-header.html"
$pandocLinkFilterPath = Join-Path $env:TEMP "lisp-pandoc-link-filter.lua"

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
	dotnet publish -c Release "/p:PublishProfile=$targetRid"

	$publishDir = Join-Path "publish" $targetRid
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

	$markdownFiles = Get-ChildItem -Path $publishDir -Filter "*.md" -Recurse -File
	foreach ($mdFile in $markdownFiles) {
		$htmlPath = [System.IO.Path]::ChangeExtension($mdFile.FullName, ".html")
		pandoc $mdFile.FullName --standalone --include-in-header=$pandocHeaderPath --lua-filter=$pandocLinkFilterPath --output $htmlPath
	}
}

if (Test-Path $pandocHeaderPath) {
	Remove-Item -Path $pandocHeaderPath -Force
}

if (Test-Path $pandocLinkFilterPath) {
	Remove-Item -Path $pandocLinkFilterPath -Force
}
