# Modifications

Ce fichier s’affiche dans TURBiX, sur la page « Nouveautés ». Chaque version a sa section,
avec les changements utiles résumés en quelques mots. Chaque version commence par
`## X.Y.Z` (sans date) et chaque changement est une puce `- **Titre.** Courte description.`

## 1.2.4

- **Tests et chiffres de performance.** Le README et le site expliquent désormais que chaque version est essayée sur des machines virtuelles propres avec peu de ressources, et montrent ce qui a été mesuré avec l’optimisation complète : la mémoire au repos a baissé de 31 %. Ce sont des chiffres approximatifs issus d’un environnement virtuel.

## 1.2.3

- **Réseau testé sous Windows 11.** Le trafic par programme, les connexions et les blocages de la section « Réseau » ont aussi été vérifiés sous Windows 11.

## 1.2.2

- **Avertissement à la désinstallation.** Si TURBiX a des blocages réseau dans le Pare-feu Windows, le programme de désinstallation le signale : ils restent actifs tant que vous ne les retirez pas depuis l’onglet « Blocages ».

## 1.2.1

- **Nouvelle section « Réseau ».** Elle montre quels programmes utilisent le réseau et combien ils reçoivent et envoient, les connexions ouvertes (que vous pouvez fermer) et les ports qui attendent des connexions de l’extérieur. Vous pouvez y bloquer un programme, une adresse ou un port avec le Pare-feu Windows et annuler quand vous voulez dans l’onglet « Blocages ». Elle ne mesure le trafic que lorsque la section est ouverte et n’utilise pas Internet.

## 1.2.0

- **Nouveau site du projet.** TURBiX a maintenant sa propre page, en six langues : https://julianr87.github.io/TURBiX/
- **Textes plus propres.** La ponctuation des aides, avertissements et messages du programme a été revue dans les six langues.

## 1.1.10

- **Installation silencieuse.** `TURBiX-Setup.exe --silent` installe sans afficher de fenêtre et `--uninstall --silent` désinstalle sans poser de question. Pratique pour équiper plusieurs PC et pour les gestionnaires de paquets.
- **Corrections après des tests sous Windows 10 et 11.** Ouvrir la fenêtre alors que TURBiX fonctionnait déjà à côté de l’horloge affichait un message « Erreur inattendue », ce n’est plus le cas. Sur les petits écrans, la fenêtre ne dépasse plus par le haut. Les notifications de Windows affichent désormais le nom TURBiX.

## 1.1.9

- **Nouveau nom : TURBiX.** Le programme, le programme d’installation, les raccourcis et la documentation s’écrivent désormais TURBiX. Les fichiers s’appellent maintenant `TURBiX.exe` et `TURBiX-Setup.exe`, vos paramètres et vos sauvegardes restent où ils étaient.

## 1.1.8

- **Documentation en six langues.** La page du projet peut maintenant être lue dans les mêmes langues que TURBiX, avec plus de détails sur chaque fonction. Le programme fonctionne comme la version 1.1.7.

## 1.1.7

- **Programme d’installation.** En plus du `TURBiX.exe` habituel, il existe maintenant un `TURBiX-Setup.exe` qui l’installe dans Program Files, crée le raccourci du menu Démarrer (et celui du bureau, si vous le souhaitez) et l’ajoute à la liste des applications de Windows.
- **Désinstallation.** TURBiX se supprime depuis Paramètres › Applications, comme n’importe quel programme. La désinstallation n’annule pas les modifications apportées à Windows (c’est le rôle de « Restaurer ») et conserve les sauvegardes.
- **Corrections.** Dans les Paramètres, « Comme Windows » affiche désormais vraiment la langue de Windows. Le journal d’activité change de fichier chaque jour, même si TURBiX reste ouvert. Le menu de l’icône ne permet plus de lancer une tâche ni de quitter tant qu’une question attend une réponse. Dans « Registre Windows », une même entrée n’apparaît plus deux fois.
## 1.1.6

- **Nouvelle page Paramètres.** Dans la barre latérale, « Apparence » devient « Paramètres ». On y trouve le thème, la langue (que vous pouvez maintenant choisir), le démarrage avec Windows, le nettoyage automatique de la mémoire, le dossier de données et un bouton pour tout remettre comme à l’origine.
- **Icône à côté de l’horloge.** Tant que TURBiX est ouvert, son icône s’affiche dans la zone de notification. Un double-clic ouvre la fenêtre, le clic droit permet de libérer la mémoire vive, de nettoyer les fichiers temporaires ou de quitter. Vous pouvez la masquer dans les Paramètres.
- **Le X ne ferme plus TURBiX.** Fermer la fenêtre la masque et TURBiX continue à fonctionner à côté de l’horloge (modifiable dans les Paramètres). Pour le fermer complètement, choisissez « Quitter TURBiX » dans le menu de l’icône, qui demande confirmation. Au démarrage de Windows, TURBiX se place désormais directement à côté de l’horloge, sans ouvrir la fenêtre.

## 1.1.5

- **Notifications Windows.** Le message de mémoire libérée arrive désormais sous forme de notification Windows, avec le nom et l’icône de TURBiX, au lieu de s’afficher dans le programme.
- **Ouvrir TURBiX au démarrage de Windows.** Nouvelle option dans Performances › Démarrage de Windows (et dans Mémoire vive). TURBiX s’ouvre réduit à l’ouverture de session, pour que le nettoyage automatique de la mémoire fonctionne toujours. Comme il a besoin des droits d’administrateur, il utilise une tâche du Planificateur de tâches nommée « TURBiX », supprimée quand vous désactivez l’option.

## 1.1.4

- **Message après la libération de la mémoire.** Quand vous nettoyez la RAM, un message indique la quantité libérée et ce qui reste disponible. Si le nettoyage automatique se lance pendant que TURBiX est réduit, il arrive sous forme de notification Windows, vous pouvez le désactiver dans « Nettoyage automatique ».

## 1.1.3

- **Vérifié sous Windows 11.** Tout a été testé à nouveau sur un Windows 11 fraîchement installé : appliquer les réglages, redémarrer et les annuler depuis « Restaurer ». Correction d’un nom de sauvegarde qui pouvait s’afficher sans traduction.
- **Petits écrans.** Dans Processus, les noms longs sont désormais raccourcis avec « … » au lieu d’être coupés, et les liens de l’Accueil ne sont plus tronqués dans une fenêtre étroite.

## 1.1.2

- **Nouveautés dans votre langue.** Avec Windows en espagnol, cette page s’affichait en anglais. Elle suit désormais la même langue que le reste de TURBiX.

## 1.1.1

- **TURBiX dans votre langue.** L’interface s’affiche d’elle-même dans la langue de Windows : espagnol, anglais, portugais, français, allemand ou russe. Pour toute autre langue, elle s’affiche en anglais.
- **Processus regroupés.** Un nouvel onglet dans Performances liste tous les processus ouverts, comme le Gestionnaire des tâches, mais avec les processus lancés par chaque programme rangés sous lui. Il affiche le processeur, la mémoire et les threads, indique quel service tourne dans chaque « Hôte de service » et permet de fermer une tâche. Les processus dont Windows a besoin sont verrouillés.
- **Le journal se souvient.** Le Journal d’activité montre aussi ce que TURBiX a fait lors des sessions précédentes, classé par jour.
- **Noms lisibles en mode sombre.** Dans « Programmes installés », les noms s’affichaient en noir sur le fond sombre.

## 1.1.0

- **Nouvelle section Applications.** Supprime en un clic les applications que Windows installe de lui-même (Candy Crush, le Solitaire avec publicités, Clipchamp, Actualités, TikTok et autres promotions). Seules des applications connues, qu’on peut retirer sans rien casser, sont proposées, jamais le Store, Xbox ni des éléments de Windows. Si l’une d’elles vous manque, vous pouvez la réinstaller gratuitement depuis le Microsoft Store.
- **Désinstaller des programmes.** La liste des programmes installés, avec leur taille, une recherche et un bouton qui ouvre le programme de désinstallation officiel de chacun.
- **Plus de réglages Windows.** Barre des tâches et menu Démarrer (boutons Conférence maintenant, Conversation, Cortana et Affichage des tâches, recherche en icône, barre alignée à gauche, fichiers récents, raccourci Edge qui revient après chaque mise à jour), services facultatifs que presque personne n’utilise (Télécopie, mode démo en magasin, cartes hors connexion, spouleur d’impression, indexation…) et désactivation de la mise en veille prolongée, qui libère plusieurs Go. Tout s’annule depuis « Restaurer ».
- **Des sauvegardes qui ne s’écrasent plus.** Si deux groupes de réglages étaient appliqués dans la même seconde (par exemple performances et réglages Windows), la sauvegarde du premier était perdue et on ne pouvait plus l’annuler depuis « Restaurer ». Désormais, chaque session a son propre fichier.
- **Testé aussi sur Windows 11.** Noms corrects dans le démarrage de Windows (Cortana apparaissait sous forme de code) et textes qui ne sont plus coupés dans Restaurer, Réparer et l’écran d’accueil.
- **Nettoyage de Windows.old.** Supprime l’ancienne installation de Windows et les restes des grosses mises à jour, qui occupent souvent 10 à 30 Go, avec le Nettoyage de disque de Windows.

## 1.0.10

- **Testé à fond sur un Windows fraîchement installé.** Chaque réglage, le nettoyage, la mémoire et le démarrage de Windows ont été appliqués, vérifiés après un redémarrage, puis annulés. Tout a bien été conservé, et l’annulation a remis l’ordinateur exactement dans son état d’origine.

## 1.0.9

- **Réparation du registre plus prudente.** Sur un Windows fraîchement installé, elle signalait comme cassées des entrées .NET que Windows livre telles quelles. Désormais, elle ne touche jamais à ce qui appartient au dossier Windows et n’affiche plus d’entrées en double.
- **Mode Jeu bien détecté.** Windows l’active par défaut, mais TURBiX l’affichait comme « Non appliqué ».
- **Processeur dans Santé.** Compte correctement les cœurs sur les ordinateurs à plusieurs processeurs et affiche le nom complet du modèle.
- **Messages plus clairs.** Si le point de restauration ne peut pas être créé, le journal d’activité en donne la raison au lieu de rester vide.

## 1.0.8

- **Icônes correctes sur Windows 10.** L’icône de la mémoire vive n’existait pas sous Windows 10 et s’affichait comme un carré vide. Elle s’affiche maintenant correctement sur toutes les versions.
- **Nouveautés sans dates.** L’historique n’affiche que le numéro de version.

## 1.0.7

- **Nouvelle section Santé.** Affiche l’état de chaque composant : disques (santé, usure, température et heures d’utilisation), batterie, mémoire vive, processeur, carte graphique et son pilote, périphériques en erreur, ainsi que les écrans bleus et arrêts inattendus du dernier mois. Chaque alerte explique quoi faire.
- **Réparer le registre Windows.** Dans Dépannage › Registre Windows : recherche les entrées qui pointent vers des programmes qui n’existent plus (éléments de démarrage cassés, programmes « fantômes » dans la liste de désinstallation, restes de programmes supprimés) et les retire, après en avoir fait une sauvegarde que vous pouvez restaurer.

## 1.0.6

- **Diagnostic de l’ordinateur.** L’écran d’accueil vérifie les causes les plus courantes d’un PC lent (disque dur mécanique ou défaillant, peu d’espace libre, peu de mémoire, trop de programmes au démarrage, plusieurs jours sans redémarrer) et vous dit quoi faire pour chacune.
- **Nouvelle section Dépannage › Réparer.** Les réparations que recommande le support Microsoft, chacune avec son bouton : fichiers Windows (SFC), image Windows (DISM), vérification du disque, réinitialisation du réseau, déblocage de l’imprimante et redémarrage de l’Explorateur.
- **Dépannage › Réglages Windows.** Supprime les petites gênes de Windows 11 : menu contextuel classique, extensions de fichiers visibles, points forts de la recherche, notifications de compte dans le menu Démarrer, « Fin de tâche » dans la barre des tâches, Démarrage rapide et plus encore.
- **Nettoyage plus sûr.** Les pièces jointes ouvertes depuis Outlook et les dossiers épinglés dans Accès rapide ne sont plus touchés, et le nettoyage ne suit jamais les liens vers d’autres dossiers.
- **Sauvegardes et annulation plus fiables.** La valeur d’origine est enregistrée avant chaque modification. Si une annulation échoue en partie, TURBiX vous prévient et vous permet de réessayer.
- **Messages plus clairs.** TURBiX vous prévient si le point de restauration n’a pas pu être créé, et s’il a été ouvert avec un autre compte administrateur que le vôtre.
- **Utilisation au clavier.** On voit où se trouve le focus en se déplaçant avec Tab, et les aides « ? » s’ouvrent avec Entrée.

## 1.0.5

- **Nouvelle section Performances.** Trois outils pour que l’ordinateur démarre plus vite, garde de la mémoire libre et tourne mieux en jeu.
- **Démarrage de Windows.** Liste les programmes qui s’ouvrent tout seuls quand vous allumez l’ordinateur et permet de les désactiver avec un interrupteur, sans rien désinstaller.
- **Mémoire vive.** Affiche l’utilisation en direct et les programmes qui en prennent le plus. Un bouton libère de la mémoire tout de suite, et le nettoyage automatique le refait toutes les 5, 10, 15, 30 ou 60 minutes.
- **Jeux.** 13 réglages qui retirent ce qui freine Windows en jeu : enregistrement en arrière-plan, économie d’énergie, accélération de la souris, programmes en arrière-plan et plus encore. Tous s’annulent depuis « Restaurer ».

## 1.0.4

- **Historique complet.** « Nouveautés » affiche de nouveau toutes les versions, sans en sauter aucune.

## 1.0.3

- **Version installée bien visible.** L’en-tête de « Nouveautés » indique quelle version de TURBiX vous utilisez.

## 1.0.2

- **État plus clair dans le menu latéral.** Là où il était écrit « hors connexion », il est maintenant écrit « N’utilise pas Internet », pour que cela ne ressemble pas à un problème de connexion. La carte a son propre « ? » qui explique chaque information.
- **Apparence plus nette.** Le « ? » du sélecteur de thème a été retiré, car il se comprend tout seul.

## 1.0.1

- **Mode clair, sombre ou celui du système.** Nouveau sélecteur « Apparence » dans le menu latéral. Avec « Système », TURBiX suit le mode de Windows et change de lui-même quand vous le modifiez dans Paramètres.
- **Section Nouveautés.** Une nouvelle page, avant « À propos de TURBiX », avec les changements importants de chaque version.

## 1.0.0

- **Première version.** Un seul exécutable pour Windows 10 (1607 ou ultérieur) et Windows 11, sans installation ni dépendances.
- **Confidentialité et télémétrie.** 23 réglages répartis en 5 groupes, avec les profils « Recommandé » et « Maximum » et l’état actuel de chacun.
- **Nettoyage.** 20 catégories de fichiers temporaires et de cache, avec une analyse préalable en lecture seule avant toute suppression.
- **Tout est réversible.** Sauvegarde automatique de chaque valeur modifiée, annulation par session et point de restauration facultatif.
- **Une aide pour chaque option.** Un bouton « ? » explique en quelques mots ce que fait chaque réglage.
- **Journal d’activité.** Tout ce que fait TURBiX est noté à l’écran et dans C:\ProgramData\TURBiX\Logs.
