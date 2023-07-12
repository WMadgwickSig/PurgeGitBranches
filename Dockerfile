# Set the base image as the .NET 7.0 SDK (this includes the runtime)
FROM mcr.microsoft.com/dotnet/sdk:7.0 as build-env

# Copy everything and publish the release (publish implicitly restores and builds)
WORKDIR /app
COPY . ./
RUN dotnet publish ./GithubAction/GithubAction.csproj -c Release -o out --no-self-contained

# Label the container
LABEL maintainer="William Madgwick <william.madgwick@signify.co.za>"
LABEL repository="https://github.com/WMadgwickSig/PurgeGitBranches"
LABEL homepage="https://github.com/WMadgwickSig/PurgeGitBranches"

# Label as GitHub action
LABEL com.github.actions.name="Purge old branches"
# Limit to 160 characters
LABEL com.github.actions.description="A GitHub action that purges branches with activity older than a specified time."
# See branding:
# https://docs.github.com/actions/creating-actions/metadata-syntax-for-github-actions#branding
LABEL com.github.actions.icon="activity"
LABEL com.github.actions.color="blue"

# Relayer the .NET SDK, anew with the build output
FROM mcr.microsoft.com/dotnet/sdk:7.0
COPY --from=build-env /app/out .
ENTRYPOINT [ "dotnet", "/GithubAction.dll" ]