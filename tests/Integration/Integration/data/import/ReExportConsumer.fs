module ReExportConsumer

let run () =
    ReExportImport.bump ()

    ReExportImport.live
    + ReExportImport.renamed
    + ReExportImport.``my-name``
    + ReExportImport.Nested.inner
    + ReExportImport.add 1 2
    + ReExportImport.casted.value
