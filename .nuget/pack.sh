#!/bin/bash
# This script is used to generate the package for the ComposableArchitecture.
KEY=`echo $NUGET_API_KEY`
VERSION="0.0.14" 
# replace the version in the nuspec file
sed -i '' "s/<version>.*<\/version>/<version>$VERSION<\/version>/g" ComposableArchitecture.nuspec
cd ..
sed -i '' "s/<Version>.*<\/Version>/<Version>$VERSION<\/Version>/g" ComposableArchitecture.csproj
echo "Publishing ComposableArchitecture..." 
dotnet publish -c Release 
echo "Packing ComposableArchitecture..."
cd -
nuget pack ComposableArchitecture.nuspec
echo "Pushing ComposableArchitecture..."
dotnet nuget push "SelfStudio.ComposableArchitecture.$VERSION.nupkg"  -k $KEY --source  https://api.nuget.org/v3/index.json
