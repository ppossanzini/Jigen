
Version="${VARIABLE:-1.0.0}"

dotnet publish src/Server/Jigen -o publish 

podman build . -t ppossanzini/jigendb:latest -t ppossanzini/jigendb:$Version
