var app = new Vue({
    el: '#DeleteMainDiv',
    data: {
        mountFinished: false,
        deleting: false,
        returnUrl: "", //string
        error: "",
        deletionDescription: "",
        image: {
            imageId: null,
            imageName: "",
            cardCount: 1,
            currentVersionUserName: "",
            currentVersionDescription: "",
            description: "",
            source: "",
            initialUploadUtcDate: null,
            deletionAlertMessage: "",
            lastChangeUtcDate: null,
            blob: null,
        },
    },
    async mounted() {
        try {
            this.GetReturnUrlFromPageParameter();
            await this.GetImageFromPageParameter();
        }
        finally {
            this.mountFinished = true;
        }
    },
    methods: {
        async GetImageFromPageParameter() {
            imageId = document.getElementById("ImageIdInput").value;
            if (!imageId) {
                this.error = "Image not found (this page expects an image id parameter)";
                return;
            }

            await axios.get('/Media/GetImageInfoForDeletion/' + imageId)
                .then(result => {
                    this.image.imageId = imageId;
                    this.image.imageName = result.data.imageName;
                    this.image.cardCount = result.data.cardCount;
                    this.image.currentVersionUserName = result.data.currentVersionUserName;
                    this.image.currentVersionDescription = result.data.currentVersionDescription;
                    this.image.description = result.data.description;
                    this.image.source = result.data.source;
                    this.image.initialUploadUtcDate = result.data.initialUploadUtcDate;
                    this.image.deletionAlertMessage = result.data.deletionAlertMessage;
                    this.image.lastChangeUtcDate = result.data.lastChangeUtcDate;
                })
                .catch(error => {
                    tellAxiosError(error, this);
                    this.error = "Image not found: " + error;
                    return;
                });

            //Note: no await here, asynchronous FTW
            axios.get('/Learn/GetImage/' + imageId + '/2', { responseType: 'arraybuffer' })
                .then(result => {
                    this.image.blob = base64FromBytes(result.data);
                })
                .catch(error => {
                    tellAxiosError(error, this);
                });
        },
        async GetReturnUrlFromPageParameter() {
            this.returnUrl = document.getElementById("ReturnUrlInput").value;
        },
        async deleteImage() {
            if (confirm(this.image.deletionAlertMessage)) {
                this.deleting = true;
                const body = { deletionDescription: this.deletionDescription };
                await axios.post('/Media/Delete/' + this.image.imageId, body)
                    .then(result => {
                        this.deleting = false;
                        this.$bvToast.toast(result.data.toastText, {
                            title: result.data.toastTitle,
                            variant: 'success',
                            toaster: 'b-toaster-top-center',
                            solid: true,
                            autoHideDelay: 5000,
                        });
                        this.goBack();
                    })
                    .catch(error => {
                        this.deleting = false;
                        tellAxiosError(error, this);
                    })
            }

        },
        goBack() {
            window.location = this.returnUrl;
        },
        showDebugInfo() {
            return true;
        },
        dt(utcFromDotNet) {
            return dateTime(utcFromDotNet);
        },
    },
});