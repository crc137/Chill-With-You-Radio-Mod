param(
  [string]$Src = "radiostations.txt",
  [string]$Out = "radiostations.checked.txt"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (!(Test-Path $Src)) { Write-Host "ERROR: $Src not found"; exit 1 }

$lines = Get-Content $Src | Where-Object { $_ -match '^https?://[^|]+\|' }
$total = $lines.Count
$ok = New-Object System.Collections.Generic.List[string]
$skip = New-Object System.Collections.Generic.List[string]
$n = 0

foreach ($line in $lines) {
  $n++
  $parts = $line -split '\|', 4
  $url = $parts[0]
  $req = $null; $resp = $null
  try {
    $req = [Net.HttpWebRequest]::Create($url)
    $req.Method = "GET"
    $req.UserAgent = "Mozilla/5.0 (X11; Linux x86_64)"
    $req.Accept = "audio/mpeg,*/*"
    $req.Timeout = 8000
    $req.ReadWriteTimeout = 8000
    $req.AddRange(0, 131071)
    $resp = $req.GetResponse()
    $code = [int]$resp.StatusCode
    $ct = "$($resp.ContentType)"
    $stream = $resp.GetResponseStream()
    $buf = New-Object byte[] 32768
    $read = $stream.Read($buf, 0, $buf.Length)
    $mpeg = $false
    for ($i = 0; $i -lt $read - 1; $i++) {
      if ($buf[$i] -eq 0xFF -and ($buf[$i + 1] -band 0xE0) -eq 0xE0) { $mpeg = $true; break }
    }
    if ((($code -eq 200) -or ($code -eq 206)) -and ($mpeg -or $ct -like 'audio/mpeg*')) {
      Write-Host ("OK      " + $url + "  [" + $ct + "]")
      $ok.Add($line)
    } else {
      Write-Host ("SKIPPED " + $url + "  [" + $ct + "]")
      $skip.Add($line)
    }
    $stream.Close(); $resp.Close(); $resp = $null
  } catch {
    Write-Host ("FAIL    " + $url + "  (" + $_.Exception.GetBaseException().Message + ")")
    $skip.Add($line)
  } finally {
    if ($resp) { try { $resp.Close() } catch {} }
  }
  if ($n % 25 -eq 0) { Write-Host ("  ... " + $n + "/" + $total) }
}

Set-Content -Path $Out -Value $ok -Encoding UTF8
Set-Content -Path ($Out -replace '\.txt$', '.skipped.txt') -Value $skip -Encoding UTF8

Write-Host ""
Write-Host ("checked: " + $total)
Write-Host ("working: " + $ok.Count + "  ->  " + $Out)
Write-Host ("dropped: " + $skip.Count)
Write-Host ""
Write-Host ("Install: copy """ + $Out + """ to <game>\BepInEx\plugins\radiostations.txt")