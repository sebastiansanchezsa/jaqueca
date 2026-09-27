// fxc2: compila una funcion HLSL con el d3dcompiler de Microsoft bajo Wine (para el MGFXC de MonoGame en Linux).
// uso: fxc2 entrada.hlsl funcion perfil flags salida.bin
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
typedef struct Blob { struct BlobVtbl *v; } Blob;
struct BlobVtbl {
  void *qi; void *addref; ULONG (__stdcall *Release)(Blob*);
  void* (__stdcall *GetBufferPointer)(Blob*); SIZE_T (__stdcall *GetBufferSize)(Blob*);
};
typedef HRESULT (__stdcall *D3DCompileFn)(const void*, SIZE_T, const char*, const void*, void*, const char*, const char*, UINT, UINT, Blob**, Blob**);
int main(int argc, char **argv) {
  if (argc < 6) { fprintf(stderr, "uso: fxc2 in func profile flags out\n"); return 2; }
  char dll[MAX_PATH]; GetModuleFileNameA(NULL, dll, MAX_PATH);
  char *s = strrchr(dll, '\\'); strcpy(s ? s + 1 : dll, "D3DCompiler_47_cor3.dll");
  HMODULE m = LoadLibraryA(dll);
  if (!m) { fprintf(stderr, "no carga %s\n", dll); return 3; }
  D3DCompileFn comp = (D3DCompileFn)GetProcAddress(m, "D3DCompile");
  FILE *f = fopen(argv[1], "rb"); if (!f) { fprintf(stderr, "no abre %s\n", argv[1]); return 4; }
  fseek(f, 0, SEEK_END); long n = ftell(f); fseek(f, 0, SEEK_SET);
  char *src = malloc(n + 1); fread(src, 1, n, f); src[n] = 0; fclose(f);
  Blob *code = NULL, *err = NULL;
  HRESULT hr = comp(src, n, argv[1], NULL, NULL, argv[2], argv[3], (UINT)strtoul(argv[4], NULL, 10), 0, &code, &err);
  if (err) { fwrite(err->v->GetBufferPointer(err), 1, err->v->GetBufferSize(err), stderr); }
  if (FAILED(hr) || !code) { fprintf(stderr, "\nD3DCompile fallo 0x%08lx\n", hr); return 1; }
  FILE *o = fopen(argv[5], "wb"); fwrite(code->v->GetBufferPointer(code), 1, code->v->GetBufferSize(code), o); fclose(o);
  return 0;
}
