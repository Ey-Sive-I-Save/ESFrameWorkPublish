Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:SupportedKeywords = @(
    '$schema','$id','$ref','$defs','title','oneOf','allOf','if','then','type','pattern','enum','const',
    'required','properties','additionalProperties','items','uniqueItems','minItems','minLength','minimum','maximum','format'
)
$script:SchemaCache = @{}

function Read-ESJsonSchemaDocument {
    param([Parameter(Mandatory=$true)][string]$Path)
    $full = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $contentHash = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not $script:SchemaCache.ContainsKey($full) -or [string]$script:SchemaCache[$full].Hash -cne $contentHash) {
        $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($full))
        $script:SchemaCache[$full] = [pscustomobject]@{ Hash=$contentHash; Root=($raw | ConvertFrom-Json -ErrorAction Stop) }
    }
    [pscustomobject]@{ Path=$full; Root=$script:SchemaCache[$full].Root; ContentHash=$contentHash }
}

function Get-ESJsonObjectProperties {
    param([AllowNull()]$Value)
    if ($Value -is [Collections.IDictionary]) {
        return @($Value.Keys | ForEach-Object { [pscustomobject]@{ Name=[string]$_; Value=$Value[$_] } })
    }
    if ($null -ne $Value -and $null -ne $Value.PSObject) { return @($Value.PSObject.Properties) }
    return @()
}

function Get-ESJsonObjectProperty {
    param([AllowNull()]$Value, [Parameter(Mandatory=$true)][string]$Name)
    if ($Value -is [Collections.IDictionary]) {
        if ($Value.Contains($Name)) { return [pscustomobject]@{ Exists=$true; Value=$Value[$Name] } }
    } elseif ($null -ne $Value -and $null -ne $Value.PSObject) {
        $property = $Value.PSObject.Properties[$Name]
        if ($null -ne $property) { return [pscustomobject]@{ Exists=$true; Value=$property.Value } }
    }
    [pscustomobject]@{ Exists=$false; Value=$null }
}

function Resolve-ESJsonSchemaReference {
    param(
        [Parameter(Mandatory=$true)][string]$Reference,
        [Parameter(Mandatory=$true)][string]$CurrentSchemaPath,
        [Parameter(Mandatory=$true)]$CurrentRoot
    )
    $schemaPath = $CurrentSchemaPath
    $root = $CurrentRoot
    $fragment = $Reference
    if (-not $Reference.StartsWith('#')) {
        $hashIndex = $Reference.IndexOf('#')
        if ($hashIndex -le 0 -or $hashIndex -ge ($Reference.Length - 1)) { throw "Unsupported schema reference: $Reference" }
        $relativePath = $Reference.Substring(0, $hashIndex)
        $schemaDirectory = [IO.Path]::GetFullPath((Split-Path -Parent $CurrentSchemaPath)).TrimEnd('\','/')
        $schemaPath = [IO.Path]::GetFullPath((Join-Path $schemaDirectory $relativePath))
        if (-not [string]::Equals((Split-Path -Parent $schemaPath).TrimEnd('\','/'),$schemaDirectory,[StringComparison]::OrdinalIgnoreCase)) { throw "External schema reference must remain in the current contract directory: $Reference" }
        $document = Read-ESJsonSchemaDocument $schemaPath
        $schemaPath = $document.Path
        $root = $document.Root
        $fragment = $Reference.Substring($hashIndex)
    }
    if ($fragment -cnotmatch '^#/\$defs/([^/]+)$') { throw "Unsupported schema reference: $Reference" }
    $definition = $root.'$defs'.PSObject.Properties[$Matches[1]]
    if ($null -eq $definition) { throw "Unresolved schema reference: $Reference" }
    [pscustomobject]@{ Path=$schemaPath; Root=$root; Schema=$definition.Value; Fragment=$fragment }
}

function Add-ESJsonSchemaError {
    param([Collections.Generic.List[string]]$Errors, [string]$Path, [string]$Message)
    [void]$Errors.Add("$Path`: $Message")
}

function Test-ESJsonSchemaSupportedNode {
    param($Schema, [string]$Path, [Collections.Generic.List[string]]$Errors, [string]$SchemaPath, $SchemaRoot, [Collections.Generic.HashSet[string]]$VisitedRefs)
    foreach ($property in Get-ESJsonObjectProperties $Schema) {
        if ($script:SupportedKeywords -cnotcontains [string]$property.Name) {
            Add-ESJsonSchemaError $Errors $Path "unsupported keyword '$($property.Name)'"
            continue
        }
        switch ([string]$property.Name) {
            '$defs' { foreach ($child in Get-ESJsonObjectProperties $property.Value) { Test-ESJsonSchemaSupportedNode $child.Value "$Path/`$defs/$($child.Name)" $Errors $SchemaPath $SchemaRoot $VisitedRefs } }
            'properties' { foreach ($child in Get-ESJsonObjectProperties $property.Value) { Test-ESJsonSchemaSupportedNode $child.Value "$Path/properties/$($child.Name)" $Errors $SchemaPath $SchemaRoot $VisitedRefs } }
            'oneOf' { for ($i=0; $i -lt @($property.Value).Count; $i++) { Test-ESJsonSchemaSupportedNode @($property.Value)[$i] "$Path/oneOf/$i" $Errors $SchemaPath $SchemaRoot $VisitedRefs } }
            'allOf' { for ($i=0; $i -lt @($property.Value).Count; $i++) { Test-ESJsonSchemaSupportedNode @($property.Value)[$i] "$Path/allOf/$i" $Errors $SchemaPath $SchemaRoot $VisitedRefs } }
            'items' { Test-ESJsonSchemaSupportedNode $property.Value "$Path/items" $Errors $SchemaPath $SchemaRoot $VisitedRefs }
            'if' { Test-ESJsonSchemaSupportedNode $property.Value "$Path/if" $Errors $SchemaPath $SchemaRoot $VisitedRefs }
            'then' { Test-ESJsonSchemaSupportedNode $property.Value "$Path/then" $Errors $SchemaPath $SchemaRoot $VisitedRefs }
            '$ref' {
                try {
                    $resolved = Resolve-ESJsonSchemaReference ([string]$property.Value) $SchemaPath $SchemaRoot
                    $key = $resolved.Path + $resolved.Fragment
                    if ($VisitedRefs.Add($key)) { Test-ESJsonSchemaSupportedNode $resolved.Schema $key $Errors $resolved.Path $resolved.Root $VisitedRefs }
                } catch { Add-ESJsonSchemaError $Errors $Path $_.Exception.Message }
            }
        }
    }
}

function Test-ESJsonType {
    param([AllowNull()]$Value, [string]$Type)
    switch ($Type) {
        'null' { return $null -eq $Value }
        'object' { return $null -ne $Value -and ($Value -is [pscustomobject] -or $Value -is [Collections.IDictionary]) }
        'array' { return $null -ne $Value -and $Value -is [Array] }
        'string' { return $Value -is [string] }
        'boolean' { return $Value -is [bool] }
        'integer' { return $Value -is [byte] -or $Value -is [int16] -or $Value -is [int32] -or $Value -is [int64] -or $Value -is [uint16] -or $Value -is [uint32] -or $Value -is [uint64] }
        'number' { return $Value -is [ValueType] -and -not ($Value -is [bool]) }
        default { throw "Unsupported JSON type: $Type" }
    }
}

function Test-ESJsonSchemaNode {
    param($Value, $Schema, [string]$Path, [Collections.Generic.List[string]]$Errors, [string]$SchemaPath, $SchemaRoot)
    $ref = Get-ESJsonObjectProperty $Schema '$ref'
    if ($ref.Exists) {
        try {
            $resolved = Resolve-ESJsonSchemaReference ([string]$ref.Value) $SchemaPath $SchemaRoot
            Test-ESJsonSchemaNode $Value $resolved.Schema $Path $Errors $resolved.Path $resolved.Root
        } catch { Add-ESJsonSchemaError $Errors $Path $_.Exception.Message }
        return
    }

    $oneOf = Get-ESJsonObjectProperty $Schema 'oneOf'
    if ($oneOf.Exists) {
        $matchCount = 0
        foreach ($candidate in @($oneOf.Value)) {
            $candidateErrors = [Collections.Generic.List[string]]::new()
            Test-ESJsonSchemaNode $Value $candidate $Path $candidateErrors $SchemaPath $SchemaRoot
            if ($candidateErrors.Count -eq 0) { $matchCount++ }
        }
        if ($matchCount -ne 1) { Add-ESJsonSchemaError $Errors $Path "oneOf matched $matchCount schemas instead of exactly one" }
        return
    }

    $typeProperty = Get-ESJsonObjectProperty $Schema 'type'
    if ($typeProperty.Exists) {
        $types = @($typeProperty.Value | ForEach-Object { [string]$_ })
        $typeMatched = $false
        foreach ($type in $types) { if (Test-ESJsonType $Value $type) { $typeMatched=$true; break } }
        if (-not $typeMatched) { Add-ESJsonSchemaError $Errors $Path ('type mismatch; expected ' + ($types -join '|')); return }
    }

    $const = Get-ESJsonObjectProperty $Schema 'const'
    if ($const.Exists -and (($Value | ConvertTo-Json -Compress) -cne ($const.Value | ConvertTo-Json -Compress))) { Add-ESJsonSchemaError $Errors $Path 'const mismatch' }
    $enum = Get-ESJsonObjectProperty $Schema 'enum'
    if ($enum.Exists -and @($enum.Value | Where-Object { ($_ | ConvertTo-Json -Compress) -ceq ($Value | ConvertTo-Json -Compress) }).Count -eq 0) { Add-ESJsonSchemaError $Errors $Path 'value is not in enum' }

    if ($Value -is [string]) {
        $pattern = Get-ESJsonObjectProperty $Schema 'pattern'; if ($pattern.Exists -and $Value -cnotmatch [string]$pattern.Value) { Add-ESJsonSchemaError $Errors $Path 'pattern mismatch' }
        $minLength = Get-ESJsonObjectProperty $Schema 'minLength'; if ($minLength.Exists -and $Value.Length -lt [int]$minLength.Value) { Add-ESJsonSchemaError $Errors $Path 'string is shorter than minLength' }
        $format = Get-ESJsonObjectProperty $Schema 'format'
        if ($format.Exists -and [string]$format.Value -eq 'date-time') {
            $parsed=[datetime]::MinValue
            if (-not [datetime]::TryParse($Value,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind,[ref]$parsed)) { Add-ESJsonSchemaError $Errors $Path 'invalid date-time' }
        }
    }
    if ($Value -is [ValueType] -and -not ($Value -is [bool])) {
        $minimum=Get-ESJsonObjectProperty $Schema 'minimum'; if ($minimum.Exists -and [decimal]$Value -lt [decimal]$minimum.Value) { Add-ESJsonSchemaError $Errors $Path 'value is below minimum' }
        $maximum=Get-ESJsonObjectProperty $Schema 'maximum'; if ($maximum.Exists -and [decimal]$Value -gt [decimal]$maximum.Value) { Add-ESJsonSchemaError $Errors $Path 'value is above maximum' }
    }
    if ($Value -is [Array]) {
        $minItems=Get-ESJsonObjectProperty $Schema 'minItems'
        if ($minItems.Exists -and $Value.Count -lt [int]$minItems.Value) { Add-ESJsonSchemaError $Errors $Path 'array has fewer items than minItems' }
        $unique=Get-ESJsonObjectProperty $Schema 'uniqueItems'
        if ($unique.Exists -and [bool]$unique.Value) {
            $keys=@($Value | ForEach-Object { $_ | ConvertTo-Json -Depth 40 -Compress })
            if (@($keys | Sort-Object -Unique).Count -ne $keys.Count) { Add-ESJsonSchemaError $Errors $Path 'array items are not unique' }
        }
        $items=Get-ESJsonObjectProperty $Schema 'items'
        if ($items.Exists) { for ($i=0; $i -lt $Value.Count; $i++) { Test-ESJsonSchemaNode $Value[$i] $items.Value "$Path/$i" $Errors $SchemaPath $SchemaRoot } }
    }
    if ($Value -is [pscustomobject] -or $Value -is [Collections.IDictionary]) {
        $required=Get-ESJsonObjectProperty $Schema 'required'
        if ($required.Exists) { foreach ($name in @($required.Value)) { if (-not (Get-ESJsonObjectProperty $Value ([string]$name)).Exists) { Add-ESJsonSchemaError $Errors $Path "missing required property '$name'" } } }
        $properties=Get-ESJsonObjectProperty $Schema 'properties'
        if ($properties.Exists) {
            $allowed=@((Get-ESJsonObjectProperties $properties.Value) | ForEach-Object { [string]$_.Name })
            foreach ($propertySchema in Get-ESJsonObjectProperties $properties.Value) {
                $actual=Get-ESJsonObjectProperty $Value $propertySchema.Name
                if ($actual.Exists) { Test-ESJsonSchemaNode $actual.Value $propertySchema.Value "$Path/$($propertySchema.Name)" $Errors $SchemaPath $SchemaRoot }
            }
            $additional=Get-ESJsonObjectProperty $Schema 'additionalProperties'
            if ($additional.Exists -and $additional.Value -eq $false) { foreach ($actual in Get-ESJsonObjectProperties $Value) { if ($allowed -cnotcontains [string]$actual.Name) { Add-ESJsonSchemaError $Errors $Path "additional property '$($actual.Name)' is not allowed" } } }
        }
    }

    $allOf=Get-ESJsonObjectProperty $Schema 'allOf'; if ($allOf.Exists) { foreach ($candidate in @($allOf.Value)) { Test-ESJsonSchemaNode $Value $candidate $Path $Errors $SchemaPath $SchemaRoot } }
    $ifSchema=Get-ESJsonObjectProperty $Schema 'if'
    if ($ifSchema.Exists) {
        $ifErrors=[Collections.Generic.List[string]]::new()
        Test-ESJsonSchemaNode $Value $ifSchema.Value $Path $ifErrors $SchemaPath $SchemaRoot
        if ($ifErrors.Count -eq 0) { $thenSchema=Get-ESJsonObjectProperty $Schema 'then'; if ($thenSchema.Exists) { Test-ESJsonSchemaNode $Value $thenSchema.Value $Path $Errors $SchemaPath $SchemaRoot } }
    }
}

function Test-ESJsonSchemaSupported {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$SchemaPath)
    $document=Read-ESJsonSchemaDocument $SchemaPath
    $errors=[Collections.Generic.List[string]]::new()
    $visited=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    Test-ESJsonSchemaSupportedNode $document.Root '$schema' $errors $document.Path $document.Root $visited
    @($errors)
}

function Test-ESJsonSchemaValue {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$SchemaPath, [Parameter(Mandatory=$true)]$Value, [string]$DefinitionName)
    $document=Read-ESJsonSchemaDocument $SchemaPath
    $schema=$document.Root
    if (-not [string]::IsNullOrWhiteSpace($DefinitionName)) {
        $definition=$document.Root.'$defs'.PSObject.Properties[$DefinitionName]
        if ($null -eq $definition) { return @("$`: unresolved schema definition '$DefinitionName'") }
        $schema=$definition.Value
    }
    $errors=[Collections.Generic.List[string]]::new()
    Test-ESJsonSchemaNode $Value $schema '$' $errors $document.Path $document.Root
    @($errors)
}

Export-ModuleMember -Function Test-ESJsonSchemaSupported,Test-ESJsonSchemaValue
