# syntax=docker/dockerfile:1
ARG DOTNET=10.0
FROM mcr.microsoft.com/dotnet/aspnet:$DOTNET AS base

# Temporarily switch to root for Chromium install
USER root

# Install Chrome directly instead of relying on a third-party Launchpad PPA.
RUN apt-get update \
  && apt-get install -y --no-install-recommends ca-certificates wget \
  && wget --no-verbose --output-document=/tmp/google-chrome.deb https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb \
  && apt-get install -y --no-install-recommends /tmp/google-chrome.deb \
  && google-chrome-stable --version \
  && rm -f /tmp/google-chrome.deb \
  && apt-get clean \
  && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:$DOTNET AS build-env
WORKDIR /src
COPY Tranga.sln /src
COPY API/API.csproj /src/API/API.csproj
RUN dotnet restore /src/API/API.csproj

COPY . /src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish /src/API/API.csproj -c Release --property:OutputPath=/publish -p:OpenApiGenerateDocumentsOnBuild=false -maxcpucount:1 --no-cache

FROM base AS runtime
WORKDIR /publish

# Expose port
EXPOSE 6531

# User setup
ARG UNAME=tranga
ARG UID=1000
ARG GID=1000
RUN groupadd -g $GID -o $UNAME \
  && useradd -m -u $UID -g $GID -o -s /bin/bash $UNAME \
  && mkdir /usr/share/tranga-api \
  && mkdir /Manga \
  && chown 1000:1000 /usr/share/tranga-api \
  && chown 1000:1000 /Manga \
  # Ensure Chrome is executable
  && chmod +x /usr/bin/google-chrome-stable

USER $UNAME

# Env vars for PuppeteerSharp (Chromium path + no-sandbox args)
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/google-chrome-stable
ENV CHROME_BIN=/usr/bin/google-chrome-stable
ENV PUPPETEER_ARGS="--no-sandbox --disable-setuid-sandbox --disable-dev-shm-usage --disable-gpu --no-zygote --single-process"


WORKDIR /publish
COPY --chown=1000:1000 --from=build-env /publish .

# Root for entrypoint if needed
USER 0
ENTRYPOINT ["dotnet", "/publish/API.dll"]
CMD [""]
