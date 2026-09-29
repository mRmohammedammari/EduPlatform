window.eduPlatformSession = {
    cookieOptions: function () {
        return "; path=/; max-age=86400; SameSite=Lax" +
            (window.location.protocol === "https:" ? "; Secure" : "");
    },
    get: function () {
        const prefix = "eduplatform-session-id=";
        const cookie = document.cookie.split(";").map(value => value.trim()).find(value => value.startsWith(prefix));
        return cookie ? decodeURIComponent(cookie.substring(prefix.length)) : "";
    },
    set: function (value) {
        document.cookie = "eduplatform-session-id=" + encodeURIComponent(value) + this.cookieOptions();
    },
    clear: function () {
        document.cookie = "eduplatform-session-id=; max-age=0" + this.cookieOptions();
    },
    download: function (fileName, bytes) {
        const blob = new Blob([new Uint8Array(bytes)], { type: "application/octet-stream" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        anchor.click();
        URL.revokeObjectURL(url);
    },
    notifications: {
        connection: null,
        connect: async function (hubUrl, token, dotNetReference) {
            if (!window.signalR || this.connection) {
                return;
            }

            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl, { accessTokenFactory: () => token })
                .withAutomaticReconnect()
                .build();
            this.connection.on("ReceiveNotification", notification =>
                dotNetReference.invokeMethodAsync("ReceiveNotification", notification));
            await this.connection.start();
        },
        disconnect: async function () {
            if (this.connection) {
                await this.connection.stop();
                this.connection = null;
            }
        }
    }
};
