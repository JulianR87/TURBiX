# Alterações

Este arquivo aparece dentro do TURBiX, na seção “Novidades”. Cada versão tem sua própria seção,
com as mudanças que importam, em poucas palavras. Cada versão começa com
`## X.Y.Z` (sem data) e cada mudança é um item `- **Título.** Descrição curta.`

## 1.2.5

- **Números do Windows 11 também.** O README e o site agora mostram o que foi medido no Windows 11 com a otimização completa: a memória em repouso caiu 22 %, os processos 14 % e os serviços 7 %. Continuam sendo números aproximados de um ambiente virtual.

## 1.2.4

- **Testes e números de desempenho.** O README e o site agora explicam que cada versão é testada em máquinas virtuais limpas com poucos recursos, e mostram o que foi medido com a otimização completa: a memória em repouso caiu 31 %. São números aproximados de um ambiente virtual.

## 1.2.3

- **Rede testada no Windows 11.** O tráfego por programa, as conexões e os bloqueios da seção «Rede» também foram verificados no Windows 11.

## 1.2.2

- **Aviso ao desinstalar.** Se o TURBiX tiver bloqueios de rede no Firewall do Windows, o desinstalador avisa: eles continuam ativos até você removê-los na aba «Bloqueios».

## 1.2.1

- **Nova seção “Rede”.** Mostra quais programas usam a rede e quanto baixam e enviam, as conexões abertas (que você pode fechar) e as portas que esperam conexões de fora. Dali você pode bloquear um programa, um endereço ou uma porta com o Firewall do Windows e desfazer quando quiser na aba “Bloqueios”. Ela mede o tráfego só enquanto a seção está aberta e não usa a internet.

## 1.2.0

- **Novo site do projeto.** O TURBiX agora tem uma página própria, em seis idiomas: https://julianr87.github.io/TURBiX/
- **Textos mais limpos.** A pontuação das ajudas, avisos e mensagens do programa foi revisada nos seis idiomas.

## 1.1.10

- **Instalação silenciosa.** `TURBiX-Setup.exe --silent` instala sem mostrar janelas e `--uninstall --silent` desinstala sem perguntar. Serve para instalar em vários computadores e para gerenciadores de pacotes.
- **Correções após testes no Windows 10 e 11.** Ao abrir a janela com o TURBiX já funcionando ao lado do relógio, aparecia um aviso de “Erro inesperado”, isso não acontece mais. Em telas pequenas a janela não sai mais pela parte de cima. As notificações do Windows agora mostram o nome TURBiX.

## 1.1.9

- **Novo nome: TURBiX.** O programa, o instalador, os atalhos e a documentação agora são escritos TURBiX. Os arquivos passam a se chamar `TURBiX.exe` e `TURBiX-Setup.exe`, seus ajustes e cópias de segurança continuam onde estavam.

## 1.1.8

- **Documentação em seis idiomas.** A página do projeto agora pode ser lida nos mesmos idiomas do TURBiX, com mais detalhes sobre cada função. O programa funciona igual à versão 1.1.7.

## 1.1.7

- **Instalador.** Além do `TURBiX.exe` de sempre, agora existe um `TURBiX-Setup.exe` que o instala em Arquivos de Programas, cria o atalho no menu Iniciar (e na área de trabalho, se você quiser) e o adiciona à lista de aplicativos do Windows.
- **Desinstalador.** O TURBiX é removido em Configurações › Aplicativos, como qualquer programa. Desinstalar não desfaz as mudanças feitas no Windows (para isso existe o “Restaurar”) e mantém as cópias de segurança.
- **Correções.** Nos Ajustes, “Igual ao Windows” agora mostra de fato o idioma do Windows. O registro de atividade passa para um arquivo novo a cada dia, mesmo com o TURBiX aberto. O menu do ícone não deixa mais iniciar uma tarefa nem sair enquanto há uma pergunta sem resposta. Em “Registro do Windows”, a mesma entrada não aparece mais duas vezes.
## 1.1.6

- **Nova página Ajustes.** Na barra lateral, “Aparência” virou “Ajustes”. Lá ficam o tema, o idioma (agora dá para escolher), a inicialização com o Windows, a liberação automática de memória, a pasta de dados e um botão para voltar tudo ao padrão.
- **Ícone ao lado do relógio.** Enquanto o TURBiX está aberto, o ícone dele aparece na área de notificação. Clique duas vezes para abrir a janela, com o botão direito dá para liberar memória RAM, limpar os arquivos temporários ou sair. Dá para escondê-lo nos Ajustes.
- **O X não fecha mais o TURBiX.** Fechar a janela só a esconde, e o TURBiX continua funcionando ao lado do relógio (dá para mudar nos Ajustes). Para fechá-lo de vez, use “Sair do TURBiX” no menu do ícone, que pede confirmação. Ao iniciar o Windows, o TURBiX agora fica direto ao lado do relógio, sem abrir a janela.

## 1.1.5

- **Notificações do Windows.** O aviso de memória liberada agora chega como notificação do Windows, com o nome e o ícone do TURBiX, em vez de aparecer dentro do programa.
- **Abrir o TURBiX ao iniciar o Windows.** Nova opção em Desempenho › Inicialização (e em Memória RAM). O TURBiX abre minimizado ao entrar no Windows, para que a liberação automática de memória funcione sempre. Como precisa de permissões de administrador, usa uma tarefa do Agendador de Tarefas chamada “TURBiX”, que é apagada ao desativar a opção.

## 1.1.4

- **Aviso ao liberar memória.** Ao limpar a memória RAM, aparece um aviso com quanto foi liberado e quanto ficou disponível. Se a limpeza automática rodar com o TURBiX minimizado, ele chega como notificação do Windows, dá para desligar em “Liberação automática”.

## 1.1.3

- **Revisado no Windows 11.** Tudo foi testado de novo em um Windows 11 recém-instalado: aplicar os ajustes, reiniciar e desfazê-los em “Restaurar”. Foi corrigido o nome de um backup que podia aparecer sem tradução.
- **Telas pequenas.** Em Processos, nomes longos agora são encurtados com “…” em vez de cortados, e os links do Início não ficam mais cortados em janelas estreitas.

## 1.1.2

- **Novidades no seu idioma.** Com o Windows em espanhol, esta página aparecia em inglês. Agora ela segue o mesmo idioma do resto do TURBiX.

## 1.1.1

- **O TURBiX no seu idioma.** O programa usa automaticamente o idioma do Windows: espanhol, inglês, português, francês, alemão ou russo. Em qualquer outro idioma, aparece em inglês.
- **Processos agrupados.** Nova guia em Desempenho com todos os processos abertos, como no Gerenciador de Tarefas, mas com os processos que cada programa abriu agrupados embaixo dele. Mostra processador, memória e threads, diz qual serviço roda em cada “Host de Serviço” e permite finalizar uma tarefa. Os processos de que o Windows precisa ficam bloqueados.
- **O registro lembra o que já foi feito.** O Registro de atividades agora mostra também o que o TURBiX fez em sessões anteriores, separado por dia.
- **Nomes legíveis no modo escuro.** Em “Programas instalados”, os nomes apareciam em preto sobre o fundo escuro.

## 1.1.0

- **Nova seção Aplicativos.** Remove com um clique os aplicativos que o Windows instala por conta própria (Candy Crush, Paciência com anúncios, Clipchamp, Notícias, TikTok e outras promoções). Só aparecem aplicativos conhecidos que podem ser removidos sem quebrar nada, nunca a Loja, o Xbox ou partes do Windows. Se sentir falta de algum, você pode reinstalá-lo de graça pela Microsoft Store.
- **Desinstalar programas.** A lista de programas instalados, com o tamanho de cada um, uma busca e um botão que abre o desinstalador oficial do programa.
- **Mais ajustes do Windows.** Barra de tarefas e Iniciar (botões Reunir Agora, Chat, Cortana e Visão de Tarefas, pesquisa como ícone, barra à esquerda, arquivos recentes, o atalho do Edge que volta a cada atualização), serviços opcionais que quase ninguém usa (Fax, modo de demonstração de loja, mapas offline, spooler de impressão, indexação…) e desativar a hibernação, que libera vários GB. Tudo pode ser desfeito em “Restaurar”.
- **Backups que não se sobrescrevem.** Se dois grupos de ajustes fossem aplicados no mesmo segundo (por exemplo, desempenho e ajustes do Windows), o backup do primeiro se perdia e não dava para desfazê-lo em “Restaurar”. Agora cada sessão tem seu próprio arquivo.
- **Testado também no Windows 11.** Nomes corretos na inicialização do Windows (a Cortana aparecia como um código) e textos que não ficam mais cortados em Restaurar, Reparar e na tela Início.
- **Limpeza do Windows.old.** Apaga a instalação anterior do Windows e as sobras das grandes atualizações, que costumam ocupar de 10 a 30 GB, usando a própria Limpeza de Disco do Windows.

## 1.0.10

- **Testado a fundo em um Windows recém-instalado.** Cada ajuste, a limpeza, a memória e a inicialização do Windows foram aplicados, conferidos depois de reiniciar o computador e desfeitos. Tudo ficou salvo, e desfazer deixou o computador exatamente como estava.

## 1.0.9

- **Reparo do registro mais cuidadoso.** Em um Windows recém-instalado, ele marcava como quebradas algumas entradas do .NET que o Windows já traz assim de fábrica. Agora nunca mexe em nada que pertença à pasta do Windows e não mostra mais entradas repetidas.
- **Modo de Jogo detectado corretamente.** O Windows já vem com ele ativado, mas o TURBiX o mostrava como “Não aplicado”.
- **Processador em Saúde.** Conta os núcleos corretamente em computadores com mais de um processador e mostra o modelo completo.
- **Avisos mais claros.** Se não for possível criar o ponto de restauração, o registro de atividades diz o motivo em vez de ficar em branco.

## 1.0.8

- **Ícones corretos no Windows 10.** O ícone da memória RAM não existia no Windows 10 e aparecia como um quadrado vazio. Agora aparece certo em todas as versões.
- **Novidades sem datas.** O histórico mostra apenas o número da versão.

## 1.0.7

- **Nova seção Saúde.** Mostra o estado de cada componente: discos (saúde, desgaste, temperatura e horas de uso), bateria, memória RAM, processador, placa de vídeo e seu driver, dispositivos com problemas e as telas azuis ou desligamentos inesperados do último mês. Cada aviso explica o que fazer.
- **Reparar o registro do Windows.** Em Soluções › Registro do Windows: procura entradas que apontam para programas que não existem mais (itens de inicialização quebrados, programas “fantasmas” na lista de desinstalação, restos de programas apagados) e as remove, salvando antes um backup que pode ser restaurado.

## 1.0.6

- **Diagnóstico do computador.** A tela Início verifica as causas mais comuns de um PC lento (HD mecânico ou com falhas, pouco espaço, pouca memória, programas demais na inicialização, dias sem reiniciar) e diz o que fazer em cada caso.
- **Nova seção Soluções › Reparar.** Os reparos que o suporte da Microsoft recomenda, com um botão cada: arquivos do Windows (SFC), imagem do Windows (DISM), verificar o disco, redefinir a rede, destravar a impressora e reiniciar o Explorador.
- **Soluções › Ajustes do Windows.** Tira incômodos do Windows 11: menu de contexto clássico, extensões de arquivo visíveis, destaques da pesquisa, avisos da conta no Iniciar, “Finalizar tarefa” na barra de tarefas, Inicialização Rápida e mais.
- **Limpeza mais segura.** Os anexos abertos pelo Outlook e as pastas fixadas no Acesso rápido não são mais tocados, e a limpeza nunca segue atalhos para outras pastas.
- **Backups e desfazer mais confiáveis.** O valor original é salvo antes de cada mudança. Se desfazer falhar em parte, o TURBiX avisa e deixa tentar de novo.
- **Avisos mais claros.** O TURBiX avisa se não conseguiu criar o ponto de restauração e se foi aberto com uma conta de administrador diferente da sua.
- **Uso pelo teclado.** Dá para ver onde está o foco ao navegar com Tab, e as ajudas “?” abrem com Enter.

## 1.0.5

- **Nova seção Desempenho.** Três ferramentas para o computador iniciar mais rápido, ter memória livre e render melhor nos jogos.
- **Inicialização do Windows.** Lista os programas que abrem sozinhos ao ligar o computador e permite desativá-los com um botão, sem desinstalar nada.
- **Memória RAM.** Mostra o uso em tempo real e quais programas ocupam mais. Um botão libera memória na hora, e a limpeza automática repete isso a cada 5, 10, 15, 30 ou 60 minutos.
- **Jogos.** 13 ajustes que tiram o que atrapalha o Windows nos jogos: gravação em segundo plano, economia de energia, aceleração do mouse, programas em segundo plano e mais. Todos podem ser desfeitos em “Restaurar”.

## 1.0.4

- **Histórico completo.** “Novidades” volta a mostrar todas as versões, sem pular nenhuma.

## 1.0.3

- **Versão instalada à vista.** O topo de “Novidades” mostra qual versão do TURBiX você está usando.

## 1.0.2

- **Status mais claro no menu lateral.** Onde antes dizia “offline”, agora diz “Não usa a internet”, para não parecer um problema na sua conexão. O cartão tem seu próprio “?” que explica cada item.
- **Visual mais limpo.** O “?” do seletor de tema foi removido, porque ele já se explica sozinho.

## 1.0.1

- **Modo claro, escuro ou do sistema.** Novo seletor “Aparência” no menu lateral. Com “Sistema”, o TURBiX usa o mesmo modo do Windows e muda sozinho quando você o altera em Configurações.
- **Seção Novidades.** Nova página, antes de “Sobre o TURBiX”, com as mudanças importantes de cada versão.

## 1.0.0

- **Primeira versão.** Um único executável para Windows 10 (1607 ou posterior) e Windows 11, sem instalação nem dependências.
- **Privacidade e telemetria.** 23 ajustes em 5 grupos, com perfis “Recomendado” e “Máximo” e o estado atual de cada um.
- **Limpeza.** 20 categorias de arquivos temporários e cache, com uma análise prévia, só de leitura, antes de apagar qualquer coisa.
- **Tudo pode ser desfeito.** Backup automático de cada valor alterado, reversão por sessão e ponto de restauração opcional.
- **Ajuda em cada opção.** Um botão “?” explica rapidamente o que cada ajuste faz.
- **Registro de atividades.** Tudo o que o TURBiX faz fica anotado na tela e em C:\ProgramData\TURBiX\Logs.
