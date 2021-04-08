
# Dev Guide

```
cd thebfg
```

* Test tool locally before push to nuget 
  ```  
  dotnet pack
  dotnet tool update --tool-path c:\dotnet-tools --add-source ./src/nupkg theBfg --version 1.0.0-beta  
  ```