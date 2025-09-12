namespace RZPrime.DTOs.Contracts
{
    public static class TokenForwardSaleAbi
    {
        public const string Value = @"
    [
    {
        ""type"": ""function"",
        ""name"": ""executeOrder"",
        ""inputs"": [
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""getOrder"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            }
        ],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""tuple"",
                ""internalType"": ""struct TokenForwardSale.Order"",
                ""components"": [
                    {
                        ""name"": ""buyToken"",
                        ""type"": ""address"",
                        ""internalType"": ""address""
                    },
                    {
                        ""name"": ""tokenAmount"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""payAmount"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""endAt"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""status"",
                        ""type"": ""uint8"",
                        ""internalType"": ""enum TokenForwardSale.OrderStatus""
                    }
                ]
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""registerUserOrder"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            },
            {
                ""name"": ""buyToken"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""tokenAmount"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""payAmount"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""endAt"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""event"",
        ""name"": ""OrderExecuted"",
        ""inputs"": [
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""indexed"": false,
                ""internalType"": ""string""
            },
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""payAmount"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""OrderRegistered"",
        ""inputs"": [
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""indexed"": false,
                ""internalType"": ""string""
            },
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""tokenAmount"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    }
]
    ";

        public const string ERC20Abi = @"[
    { 'constant':true,'inputs':[{'name':'_owner','type':'address'}],'name':'balanceOf','outputs':[{'name':'balance','type':'uint256'}],'type':'function' },
    { 'constant':true,'inputs':[],'name':'decimals','outputs':[{'name':'','type':'uint8'}],'type':'function' },
    { 'constant':true,'inputs':[],'name':'symbol','outputs':[{'name':'','type':'string'}],'type':'function' }
]";

    }
}
