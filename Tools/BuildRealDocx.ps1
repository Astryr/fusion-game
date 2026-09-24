$ErrorActionPreference = "Stop"

function Esc([string]$s) {
    if ($null -eq $s) { return "" }
    return (($s -replace "&", "&amp;") -replace "<", "&lt;") -replace ">", "&gt;"
}

function Para([string]$text, [int]$size, [string]$color, [bool]$bold, [int]$before, [int]$after, [int]$left, [string]$fill, [bool]$italic) {
    $b = ""
    if ($bold) { $b = "<w:b/>" }
    $i = ""
    if ($italic) { $i = "<w:i/>" }
    $ind = ""
    if ($left -gt 0) { $ind = "<w:ind w:left=`"$left`"/>" }
    $shd = ""
    if ($fill) { $shd = "<w:shd w:val=`"clear`" w:color=`"auto`" w:fill=`"$fill`"/>" }
    $safe = Esc $text
    return "<w:p><w:pPr><w:spacing w:before=`"$before`" w:after=`"$after`"/>$ind$shd</w:pPr><w:r><w:rPr>$b$i<w:color w:val=`"$color`"/><w:sz w:val=`"$size`"/><w:szCs w:val=`"$size`"/><w:rFonts w:ascii=`"Calibri`" w:hAnsi=`"Calibri`"/></w:rPr><w:t xml:space=`"preserve`">$safe</w:t></w:r></w:p>"
}

function CellXml([string]$text, [bool]$header, [int]$width) {
    $fill = "FFFFFF"
    $color = "1A1A1A"
    $b = ""
    if ($header) {
        $fill = "1F4E3C"
        $color = "FFFFFF"
        $b = "<w:b/>"
    }
    $safe = Esc $text
    return "<w:tc><w:tcPr><w:tcW w:w=`"$width`" w:type=`"dxa`"/><w:shd w:val=`"clear`" w:color=`"auto`" w:fill=`"$fill`"/></w:tcPr><w:p><w:pPr><w:spacing w:after=`"40`"/></w:pPr><w:r><w:rPr>$b<w:color w:val=`"$color`"/><w:sz w:val=`"18`"/><w:szCs w:val=`"18`"/><w:rFonts w:ascii=`"Calibri`" w:hAnsi=`"Calibri`"/></w:rPr><w:t xml:space=`"preserve`">$safe</w:t></w:r></w:p></w:tc>"
}

$src = "c:\Proyectos\fusion-game\Tools\exam-doc-content.txt"
$lines = Get-Content -LiteralPath $src -Encoding UTF8

$sb = New-Object System.Text.StringBuilder
[void]$sb.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
[void]$sb.Append('<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>')

$tableRows = New-Object System.Collections.Generic.List[object]
$inTable = $false

function FlushTable {
    param($builder, $rows)
    if ($rows.Count -eq 0) { return }
    $cols = $rows[0].Count
    $width = [int](10000 / $cols)
    [void]$builder.Append('<w:tbl><w:tblPr><w:tblW w:w="5000" w:type="pct"/><w:tblBorders><w:top w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/><w:left w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/><w:bottom w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/><w:right w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/><w:insideH w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/><w:insideV w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/></w:tblBorders></w:tblPr>')
    for ($r = 0; $r -lt $rows.Count; $r++) {
        [void]$builder.Append("<w:tr>")
        $isHeader = ($r -eq 0)
        foreach ($c in $rows[$r]) {
            [void]$builder.Append((CellXml $c $isHeader $width))
        }
        [void]$builder.Append("</w:tr>")
    }
    [void]$builder.Append("</w:tbl>")
    [void]$builder.Append((Para "" 22 "1A1A1A" $false 0 80 0 $null $false))
}

foreach ($raw in $lines) {
    if ($null -eq $raw) { continue }
    $line = [string]$raw
    if ($line.Trim().Length -eq 0) { continue }
    $pipe = $line.IndexOf("|")
    if ($pipe -lt 0) { continue }
    $kind = $line.Substring(0, $pipe)
    $rest = $line.Substring($pipe + 1)

    if ($kind -eq "TABLE") {
        $inTable = $true
        $tableRows.Clear()
        $tableRows.Add($rest.Split("|"))
        continue
    }
    if ($kind -eq "ROW") {
        $tableRows.Add($rest.Split("|"))
        continue
    }
    if ($kind -eq "TABLEEND") {
        FlushTable $sb $tableRows
        $tableRows.Clear()
        $inTable = $false
        continue
    }

    switch ($kind) {
        "H1" { [void]$sb.Append((Para $rest 32 "1F4E3C" $true 360 160 0 $null $false)) }
        "H2" { [void]$sb.Append((Para $rest 26 "2E7D4F" $true 280 120 0 $null $false)) }
        "P" { [void]$sb.Append((Para $rest 22 "1A1A1A" $false 0 140 0 $null $false)) }
        "BULLET" { [void]$sb.Append((Para ("- " + $rest) 22 "1A1A1A" $false 0 60 360 $null $false)) }
        "NOTE" { [void]$sb.Append((Para $rest 22 "1F4E3C" $false 120 160 200 "E8F3EC" $true)) }
    }
}

[void]$sb.Append('<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1008" w:right="1008" w:bottom="1008" w:left="1008"/></w:sectPr>')
[void]$sb.Append("</w:body></w:document>")

$staging = Join-Path $env:TEMP "br-real-docx"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $staging "_rels") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $staging "word\_rels") | Out-Null

$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText((Join-Path $staging "word\document.xml"), $sb.ToString(), $utf8)
[System.IO.File]::WriteAllText((Join-Path $staging "[Content_Types].xml"), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>', $utf8)
[System.IO.File]::WriteAllText((Join-Path $staging "_rels\.rels"), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>', $utf8)
[System.IO.File]::WriteAllText((Join-Path $staging "word\_rels\document.xml.rels"), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"></Relationships>', $utf8)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$dests = @(
    "c:\Proyectos\fusion-game\BananaRush-Resumen-Examen.docx",
    (Join-Path $env:USERPROFILE "Downloads\BananaRush-Resumen-Examen.docx")
)
foreach ($dest in $dests) {
    if (Test-Path $dest) { Remove-Item $dest -Force }
    $zip = [System.IO.Compression.ZipFile]::Open($dest, [System.IO.Compression.ZipArchiveMode]::Create)
    $files = @(
        @{ Src = "[Content_Types].xml"; Entry = "[Content_Types].xml" },
        @{ Src = "_rels\.rels"; Entry = "_rels/.rels" },
        @{ Src = "word\document.xml"; Entry = "word/document.xml" },
        @{ Src = "word\_rels\document.xml.rels"; Entry = "word/_rels/document.xml.rels" }
    )
    foreach ($f in $files) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $staging $f.Src), $f.Entry) | Out-Null
    }
    $zip.Dispose()
}

$txt = Join-Path $env:USERPROFILE "Downloads\BananaRush-Resumen-Examen.txt"
Copy-Item $src $txt -Force
Write-Output "OK"
Write-Output ((Get-Item $dests[0]).Length)
Write-Output $txt
