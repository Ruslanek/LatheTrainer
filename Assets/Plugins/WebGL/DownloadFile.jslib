mergeInto(LibraryManager.library, {
  DownloadFile: function (array, length, fileNamePtr, mimeTypePtr) {
    var bytes = HEAPU8.slice(array, array + length);
    var fileName = UTF8ToString(fileNamePtr);
    var mimeType = UTF8ToString(mimeTypePtr);

    var blob = new Blob([bytes], { type: mimeType });
    var url = URL.createObjectURL(blob);

    var a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    URL.revokeObjectURL(url);
  }
});