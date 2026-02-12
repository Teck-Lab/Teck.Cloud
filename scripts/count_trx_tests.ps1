$trx = Get-ChildItem -Path "TestResults" -Recurse -Filter *.trx -File -ErrorAction SilentlyContinue
if(-not $trx){ Write-Output 'No TRX files found'; exit 0 }

$tot=0; $passed=0; $failed=0; $notExecuted=0
foreach($f in $trx){
    try { $xml = [xml](Get-Content $f.FullName) } catch { Write-Warning "Failed to parse $($f.FullName): $_"; continue }
    $nodes = $xml.SelectNodes('//UnitTestResult')
    if($nodes -ne $null){
        $tot += $nodes.Count
        foreach($n in $nodes){
            switch($n.outcome){
                'Passed' { $passed++ }
                'Failed' { $failed++ }
                default { $notExecuted++ }
            }
        }
    }
}

Write-Output "Found $($trx.Count) .trx files."
Write-Output "Total tests: $tot"
Write-Output "Passed: $passed"
Write-Output "Failed: $failed"
Write-Output "NotExecuted/Other: $notExecuted"
