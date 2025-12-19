<<<<<<< Updated upstream
<<<<<<< Updated upstream
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates a random integer between 1 and a specified upper bound (inclusive).

.DESCRIPTION
    This script generates a cryptographically secure random number between 1 and x,
    where x is the supplied upper bound parameter. The result is inclusive on both ends.

    Useful for AI agents and automation scripts that need random number generation
    with validation and error handling.

.PARAMETER UpperBound
    The upper bound (x) for the random number generation. Must be a positive integer >= 1.
    The generated number will be between 1 and UpperBound (inclusive).

.PARAMETER Seed
    Optional seed value for random number generation. If not provided, uses cryptographically
    secure random number generator. Useful for reproducible results in testing.

.EXAMPLE
    .\Get-RandomNumber.ps1 -UpperBound 10
    Generates a random number between 1 and 10 (inclusive).

.EXAMPLE
    .\Get-RandomNumber.ps1 -UpperBound 100 -Seed 42
    Generates a random number between 1 and 100 using seed 42 for reproducibility.

.EXAMPLE
    .\Get-RandomNumber.ps1 50
    Generates a random number between 1 and 50 using positional parameter.

.OUTPUTS
    System.Int32
    Returns a random integer between 1 and UpperBound (inclusive).

.NOTES
    - The script uses Get-Random cmdlet which is cryptographically secure by default
    - Input validation ensures UpperBound is a positive integer >= 1
    - Exit codes: 0 = success, 1 = validation error
    - Idempotent: can be run multiple times with same input (different results unless seeded)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0, HelpMessage = "Upper bound for random number generation (must be >= 1)")]
    [ValidateScript({
        if ($_ -isnot [int] -and $_ -isnot [long] -and $_ -isnot [string]) {
            throw "UpperBound must be a numeric value"
        }
        $numValue = 0
        if ([int]::TryParse($_, [ref]$numValue)) {
            if ($numValue -lt 1) {
                throw "UpperBound must be >= 1. Provided value: $numValue"
            }
            $true
        } else {
            throw "UpperBound must be a valid integer. Provided value: $_"
        }
    })]
    [int]$UpperBound,

    [Parameter(Mandatory = $false, HelpMessage = "Optional seed for reproducible random number generation")]
    [int]$Seed
)

# Error handling: Stop on any error
$ErrorActionPreference = "Stop"

try {
    # Validate UpperBound is positive
    if ($UpperBound -lt 1) {
        Write-Error "UpperBound must be >= 1. Provided value: $UpperBound"
        exit 1
    }

    # Generate random number
    if ($PSBoundParameters.ContainsKey('Seed')) {
        # Use seeded random number generator for reproducibility
        $random = New-Object System.Random $Seed
        $result = $random.Next(1, $UpperBound + 1)
    } else {
        # Use cryptographically secure random number generator
        $result = Get-Random -Minimum 1 -Maximum ($UpperBound + 1)
    }

    # Output the result (can be captured by calling script)
    Write-Output $result
    exit 0
}
catch {
    Write-Error "Error generating random number: $_"
    exit 1
}

=======
=======
>>>>>>> Stashed changes
<#
.SYNOPSIS
    Generates a random number within a specified range.

.DESCRIPTION
    This script generates a random integer between 0 (inclusive) and the specified upper bound (exclusive).
    The upper bound must be a positive integer greater than 0.

.PARAMETER UpperBound
    The exclusive upper bound for the random number generation. Must be a positive integer.

.EXAMPLE
    .\Get-RandomNumber.ps1 -UpperBound 10
    Generates a random number between 0 and 9 (inclusive).

.EXAMPLE
    .\Get-RandomNumber.ps1 -UpperBound 100
    Generates a random number between 0 and 99 (inclusive).

.OUTPUTS
    System.Int32
    Returns a random integer in the range [0, UpperBound).
#>
param(
    [Parameter(Mandatory=$true)]
    [ValidateScript({
        if ($_ -le 0) {
            throw "UpperBound must be a positive integer greater than 0."
        }
        $true
    })]
    [int]$UpperBound
)

# Generate and return a random number between 0 (inclusive) and UpperBound (exclusive)
$randomNumber = Get-Random -Minimum 0 -Maximum $UpperBound
Write-Output $randomNumber
<<<<<<< Updated upstream
>>>>>>> Stashed changes
=======
>>>>>>> Stashed changes
