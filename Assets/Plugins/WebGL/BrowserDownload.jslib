mergeInto(LibraryManager.library, {
  SaveFileFromUnity: function (dataPtr, dataLen, fileNamePtr, mimeTypePtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var mimeType = UTF8ToString(mimeTypePtr);

    var bytes = new Uint8Array(dataLen);
    bytes.set(HEAPU8.subarray(dataPtr, dataPtr + dataLen));

    var blob = new Blob([bytes], { type: mimeType });
    var url = URL.createObjectURL(blob);

    var a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  }
});
