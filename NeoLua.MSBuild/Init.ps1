param($installPath, $toolsPath, $package)

Write-Host "Copy libs...";

if($dte.Solution -and $dte.Solution.IsOpen) 
{
	$target = Join-Path (Split-Path $dte.Solution.Properties.Item("Path").Value) ".build";
}
else
{
	throw "No solution.";
}

Write-Host "Target for msbuild task factory: $target";
if (!(Test-Path $target))
{
	mkdir $target | Out-Null
}
Copy-Item "$toolsPath\Neo.Lua.dll" $target -Force | Out-Null;
Copy-Item "$toolsPath\Neo.Lua.MSBuild.dll" $target -Force | Out-Null;
Write-Host "Copy successful...";
