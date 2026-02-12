$files = Get-ChildItem -Path "TestResults" -Recurse -Filter "coverage.cobertura.xml" -File
if (-not $files) {
    Write-Output "No coverage files found under TestResults"
    exit 0
}

$pkgTotals = @{}

foreach ($file in $files) {
    try {
        $xml = [xml](Get-Content $file.FullName)
    } catch {
        Write-Warning "Failed to parse $($file.FullName): $_"
        continue
    }
    $packages = $xml.coverage.packages.package
    foreach ($package in $packages) {
        $pkgName = $package.name
        if (-not $pkgTotals.ContainsKey($pkgName)) {
            $pkgTotals[$pkgName] = @{Covered=0; Valid=0}
        }
        $covered = 0
        $valid = 0
        foreach ($class in $package.classes.class) {
            foreach ($line in $class.lines.line) {
                $valid += 1
                $hits = 0
                if ($line.hits) { $hits = [int]$line.hits }
                if ($hits -gt 0) { $covered += 1 }
            }
        }
        $pkgTotals[$pkgName].Covered += $covered
        $pkgTotals[$pkgName].Valid += $valid
    }
}

# Prepare output
$rows = @()
foreach ($k in $pkgTotals.Keys) {
    $cov = $pkgTotals[$k].Covered
    $val = $pkgTotals[$k].Valid
    $pct = 0.0
    if ($val -gt 0) { $pct = 100.0 * $cov / $val }
    $rows += [PSCustomObject]@{Package=$k; Covered=$cov; Valid=$val; Percent=$pct}
}
$rows = $rows | Sort-Object -Property Percent -Descending

Write-Output "Found $($files.Count) coverage files. Parsed $($rows.Count) packages.`n"
Write-Output ("{0,9}  {1,12}  {2,10}  {3}" -f 'Coverage%','LinesCovered','LinesValid','Package')
Write-Output ('-'*80)
foreach ($r in $rows) {
    Write-Output ("{0,8:N2}%  {1,12}  {2,10}  {3}" -f $r.Percent,$r.Covered,$r.Valid,$r.Package)
}

$totalCov = ($rows | Measure-Object -Property Covered -Sum).Sum
$totalVal = ($rows | Measure-Object -Property Valid -Sum).Sum
$overall = 0.0
if ($totalVal -gt 0) { $overall = 100.0 * $totalCov / $totalVal }
Write-Output "`nOverall combined coverage: {0:N2}% ({1} / {2})" -f $overall,$totalCov,$totalVal
