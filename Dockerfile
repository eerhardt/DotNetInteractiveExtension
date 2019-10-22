FROM jupyter/scipy-notebook:latest

# Install .NET CLI dependencies

ARG NB_USER=jovyan
ARG NB_UID=1000
ENV USER ${NB_USER}
ENV NB_UID ${NB_UID}
ENV HOME /home/${NB_USER}

WORKDIR ${HOME}

USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
# Install .NET CLI dependencies
        libc6 \
        libgcc1 \
        libgssapi-krb5-2 \
        libicu60 \
        libssl1.1 \
        libstdc++6 \
        zlib1g \
    && rm -rf /var/lib/apt/lists/*

# Install .NET Core SDK
ENV DOTNET_SDK_VERSION 3.0.100

RUN curl -SL --output dotnet.tar.gz https://dotnetcli.blob.core.windows.net/dotnet/Sdk/$DOTNET_SDK_VERSION/dotnet-sdk-$DOTNET_SDK_VERSION-linux-x64.tar.gz \
    && dotnet_sha512='766da31f9a0bcfbf0f12c91ea68354eb509ac2111879d55b656f19299c6ea1c005d31460dac7c2a4ef82b3edfea30232c82ba301fb52c0ff268d3e3a1b73d8f7' \
    && echo "$dotnet_sha512 dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -zxf dotnet.tar.gz -C /usr/share/dotnet \
    && rm dotnet.tar.gz \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Enable detection of running in a container
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip

# Trigger first run experience by running arbitrary cmd
RUN dotnet help

# Copy notebooks, package sources, and source code
# NOTE: Do this before installing dotnet-try so we get the
# latest dotnet-try everytime we change sources.
COPY ./NotebookExamples/ ${HOME}/Notebooks/
COPY ./NuGet.config ${HOME}/nuget.config
COPY ./src/ ${HOME}/src/

RUN mkdir ${HOME}/packages/ ${HOME}/localNuget/

RUN chown -R ${NB_UID} ${HOME}
USER ${USER}

# Install Microsoft.DotNet.Interactive
RUN dotnet tool install -g dotnet-try --add-source "https://dotnet.myget.org/F/dotnet-try/api/v3/index.json"

ENV PATH="${PATH}:${HOME}/.dotnet/tools"
RUN echo "$PATH"

# Install kernel specs
RUN dotnet try jupyter install

# Build extensions
RUN dotnet build ${HOME}/src/Microsoft.ML.DotNet.Interactive.Extensions -c Release 
RUN dotnet pack ${HOME}/src/Microsoft.ML.DotNet.Interactive.Extensions -c Release 

# Publish nuget if there is any
WORKDIR ${HOME}/src/
RUN dotnet nuget push **/*.nupkg -s ${HOME}/localNuget/

RUN rm -fr ${HOME}/src/

# Set root to Notebooks
WORKDIR ${HOME}/Notebooks/
