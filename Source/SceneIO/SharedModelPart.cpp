#include "SharedModelPart.h"
#include "DataTypes.h"

/* Create the model part (a child to SharedModelBuffers) */
SharedModelPart::SharedModelPart(LoadedModelPart* _m)
{
	modelMetaData = new LoadedModelPart(*_m);

	//Create index buffer 
	D3D11_BUFFER_DESC bd;
	ZeroMemory(&bd, sizeof(bd));
	indexCount = modelMetaData->compIndices.size();
	bd.Usage = D3D11_USAGE_DEFAULT;
	bd.ByteWidth = sizeof(WORD) * indexCount;
	bd.BindFlags = D3D11_BIND_INDEX_BUFFER;
	bd.CPUAccessFlags = 0;
	D3D11_SUBRESOURCE_DATA InitData;
	ZeroMemory(&InitData, sizeof(InitData));
	InitData.pSysMem = modelMetaData->compIndices.data();
	HR(Shared::m_pDevice->CreateBuffer(&bd, &InitData, &g_pIndexBuffer));

	//Create the constant buffer 
	bd.Usage = D3D11_USAGE_DEFAULT;
	bd.ByteWidth = sizeof(ConstantBuffer);
	bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
	bd.CPUAccessFlags = 0;
	HR(Shared::m_pDevice->CreateBuffer(&bd, nullptr, &g_pConstantBuffer));
}

/* Destroy our model part */
SharedModelPart::~SharedModelPart()
{
	Memory::SafeRelease(g_pIndexBuffer);
	Memory::SafeRelease(g_pConstantBuffer);
	Memory::SafeDelete(modelMetaData);
}

/* Render our model part */
void SharedModelPart::Render(XMMATRIX world, DynamicMaterial* material)
{
	//Update and main constant buffer with world info
	int cbCount = 0;
	int texCount = 0;
	ConstantBuffer cb;
	cb.mWorld = XMMatrixTranspose(world);
	cb.mView = XMMatrixTranspose(Shared::mView);
	cb.mProjection = XMMatrixTranspose(Shared::mProjection);
	Shared::m_pImmediateContext->UpdateSubresource(g_pConstantBuffer, 0, nullptr, &cb, 0, 0);
	Shared::m_pImmediateContext->VSSetConstantBuffers(0, 1, &g_pConstantBuffer);
	Shared::m_pImmediateContext->PSSetConstantBuffers(0, 1, &g_pConstantBuffer);
	cbCount++;

	//Update dynamic material constant buffers & texture data
	for (int i = 0; i < material->GetParameterCount(); i++) {
		if (material->GetParameter(i)->isBound) {
			ID3D11Buffer* cbRef = material->GetParameter(i)->value->GetDataBindable();
			if (cbRef != nullptr) {
				Shared::m_pImmediateContext->VSSetConstantBuffers(cbCount, 1, &cbRef);
				Shared::m_pImmediateContext->PSSetConstantBuffers(cbCount, 1, &cbRef);
				cbCount++;
				continue;
			}
			//If the bound type wasn't data, it must be a shader resource view (we continue even if nullptr to keep the count correct)
			ID3D11ShaderResourceView* texRef = material->GetParameter(i)->value->GetTextureBindable();
			Shared::m_pImmediateContext->PSSetShaderResources(texCount, 1, &texRef);
			texCount++;
		}
	}

	//Set index buffer and draw
	Shared::m_pImmediateContext->IASetIndexBuffer(g_pIndexBuffer, DXGI_FORMAT_R16_UINT, 0);
	Shared::m_pImmediateContext->DrawIndexed(indexCount, 0, 0);
}
