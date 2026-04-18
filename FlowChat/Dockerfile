FROM mcr.microsoft.com/dotnet/sdk:8.0 AS base
USER root
WORKDIR /app

RUN apt update && apt install -y ffmpeg libopus-dev libsodium-dev && rm -rf /var/lib/apt/lists/*

USER $APP_UID

#RUN mkdir /app/ffmpeg-temp && \
#    mkdir /app/ffmpeg && \
#    cd /app/ffmpeg-temp && \
#    wget -O ffmpeg.tar.xz https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-amd64-static.tar.xz && \
#    tar xvf ffmpeg.tar.xz && \
#    mv ffmpeg-*-amd64-static/ffmpeg /app/ffmpeg && \
#    rm -rf /app/ffmpeg-temp

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS libdave
RUN apt update && apt install -y curl unzip && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL "https://github.com/discord/libdave/releases/download/v1.1.1%2Fcpp/libdave-Linux-X64-boringssl.zip" \
        -o /tmp/libdave.zip \
    && unzip /tmp/libdave.zip -d /tmp/libdave \
    && find /tmp/libdave -name "libdave.so" -exec cp {} /libdave.so \;

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FlowChat/FlowChat.csproj", "FlowChat/"]
RUN dotnet restore "FlowChat/FlowChat.csproj"
COPY . .
WORKDIR "/src/FlowChat"
RUN dotnet build "FlowChat.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FlowChat.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=libdave /libdave.so .
RUN chmod 755 libdave.so
#USER root
#COPY --from=libdave /libdave.so /usr/local/lib/libdave.so
#RUN ldconfig
#USER $APP_UID
ENTRYPOINT ["dotnet", "FlowChat.dll"]
